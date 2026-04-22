using System.Diagnostics;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace WearPartsControl.ApplicationServices.PlcService;

public sealed class PlcOperationPipeline : IPlcOperationPipeline
{
    private const long SlowQueueWaitThresholdMilliseconds = 100;
    private const long SlowExecutionThresholdMilliseconds = 500;

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly IPlcOperationContext _plcContext;
    private readonly ILogger<PlcOperationPipeline> _logger;
    private int _pendingOperations;
    private long _operationSequence;

    public PlcOperationPipeline(IPlcOperationContext plcContext, ILogger<PlcOperationPipeline> logger)
    {
        _plcContext = plcContext;
        _logger = logger;
    }

    public Task ExecuteAsync(string operationName, Action<IPlcOperationContext> operation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        return ExecuteAsync(operationName, plcContext =>
        {
            operation(plcContext);
            return Task.CompletedTask;
        }, cancellationToken);
    }

    public Task<TResult> ExecuteAsync<TResult>(string operationName, Func<IPlcOperationContext, TResult> operation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        return ExecuteAsync(operationName, plcContext => Task.FromResult(operation(plcContext)), cancellationToken);
    }

    public async Task ExecuteAsync(string operationName, Func<IPlcOperationContext, Task> operation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ValidateOperationName(operationName);

        var operationId = Interlocked.Increment(ref _operationSequence);
        var queuedAt = Stopwatch.GetTimestamp();
        var queueDepth = Interlocked.Increment(ref _pendingOperations);

        _logger.LogDebug("PLC pipeline queued operation {OperationName}#{OperationId}, pending {PendingOperations}", operationName, operationId, queueDepth);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        var waitElapsed = Stopwatch.GetElapsedTime(queuedAt);
        var executionStartedAt = Stopwatch.GetTimestamp();

        LogQueueWait(operationName, operationId, queueDepth, waitElapsed);

        try
        {
            await operation(_plcContext).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var executionElapsed = Stopwatch.GetElapsedTime(executionStartedAt);
            _logger.LogWarning(ex, "PLC pipeline operation {OperationName}#{OperationId} failed after {ExecutionElapsedMs} ms", operationName, operationId, executionElapsed.TotalMilliseconds);
            throw;
        }
        finally
        {
            var executionElapsed = Stopwatch.GetElapsedTime(executionStartedAt);
            LogExecution(operationName, operationId, executionElapsed);
            Interlocked.Decrement(ref _pendingOperations);
            _gate.Release();
        }
    }

    public async Task<TResult> ExecuteAsync<TResult>(string operationName, Func<IPlcOperationContext, Task<TResult>> operation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ValidateOperationName(operationName);

        var operationId = Interlocked.Increment(ref _operationSequence);
        var queuedAt = Stopwatch.GetTimestamp();
        var queueDepth = Interlocked.Increment(ref _pendingOperations);

        _logger.LogDebug("PLC pipeline queued operation {OperationName}#{OperationId}, pending {PendingOperations}", operationName, operationId, queueDepth);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        var waitElapsed = Stopwatch.GetElapsedTime(queuedAt);
        var executionStartedAt = Stopwatch.GetTimestamp();

        LogQueueWait(operationName, operationId, queueDepth, waitElapsed);

        try
        {
            return await operation(_plcContext).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var executionElapsed = Stopwatch.GetElapsedTime(executionStartedAt);
            _logger.LogWarning(ex, "PLC pipeline operation {OperationName}#{OperationId} failed after {ExecutionElapsedMs} ms", operationName, operationId, executionElapsed.TotalMilliseconds);
            throw;
        }
        finally
        {
            var executionElapsed = Stopwatch.GetElapsedTime(executionStartedAt);
            LogExecution(operationName, operationId, executionElapsed);
            Interlocked.Decrement(ref _pendingOperations);
            _gate.Release();
        }
    }

    private void LogQueueWait(string operationName, long operationId, int queueDepth, TimeSpan waitElapsed)
    {
        if (waitElapsed.TotalMilliseconds >= SlowQueueWaitThresholdMilliseconds)
        {
            _logger.LogWarning("PLC pipeline operation {OperationName}#{OperationId} waited {WaitElapsedMs} ms before execution, pending {PendingOperations}", operationName, operationId, waitElapsed.TotalMilliseconds, queueDepth);
            return;
        }

        _logger.LogDebug("PLC pipeline operation {OperationName}#{OperationId} acquired execution slot in {WaitElapsedMs} ms", operationName, operationId, waitElapsed.TotalMilliseconds);
    }

    private void LogExecution(string operationName, long operationId, TimeSpan executionElapsed)
    {
        if (executionElapsed.TotalMilliseconds >= SlowExecutionThresholdMilliseconds)
        {
            _logger.LogWarning("PLC pipeline operation {OperationName}#{OperationId} completed in {ExecutionElapsedMs} ms", operationName, operationId, executionElapsed.TotalMilliseconds);
            return;
        }

        _logger.LogDebug("PLC pipeline operation {OperationName}#{OperationId} completed in {ExecutionElapsedMs} ms", operationName, operationId, executionElapsed.TotalMilliseconds);
    }

    private static void ValidateOperationName(string operationName)
    {
        if (string.IsNullOrWhiteSpace(operationName))
        {
            throw new ArgumentException("PLC pipeline operation name is required.", nameof(operationName));
        }
    }
}