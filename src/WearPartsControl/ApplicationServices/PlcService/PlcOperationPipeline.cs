using System.Diagnostics;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace WearPartsControl.ApplicationServices.PlcService;

public sealed class PlcOperationPipeline : IPlcOperationPipeline
{
    private const long SlowQueueWaitThresholdMilliseconds = 100;
    private const long SlowExecutionThresholdMilliseconds = 500;

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly IPlcService _plcService;
    private readonly ILogger<PlcOperationPipeline> _logger;
    private int _pendingOperations;
    private long _operationSequence;

    internal PlcOperationPipeline(IPlcService plcService, ILogger<PlcOperationPipeline> logger)
    {
        _plcService = plcService;
        _logger = logger;
    }

    public Task ConnectAsync(string operationName, PlcConnectionOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        return ExecuteCoreAsync(operationName, plcService => plcService.ConnectAsync(options, cancellationToken), cancellationToken);
    }

    public Task DisconnectAsync(string operationName, CancellationToken cancellationToken = default)
    {
        return ExecuteCoreAsync(operationName, plcService =>
        {
            plcService.Disconnect();
            return Task.CompletedTask;
        }, cancellationToken);
    }

    public Task<bool> IsConnectedAsync(string operationName, CancellationToken cancellationToken = default)
    {
        return ExecuteCoreAsync(operationName, plcService => Task.FromResult(plcService.IsConnected), cancellationToken);
    }

    public Task<TValue> ReadAsync<TValue>(string operationName, string address, int retryCount = 1, CancellationToken cancellationToken = default)
    {
        return ExecuteCoreAsync(operationName, plcService => Task.FromResult(plcService.Read<TValue>(address, retryCount)), cancellationToken);
    }

    public Task WriteAsync<TValue>(string operationName, string address, TValue value, int retryCount = 1, CancellationToken cancellationToken = default)
    {
        return ExecuteCoreAsync(operationName, plcService =>
        {
            plcService.Write(address, value, retryCount);
            return Task.CompletedTask;
        }, cancellationToken);
    }

    private async Task ExecuteCoreAsync(string operationName, Func<IPlcService, Task> operation, CancellationToken cancellationToken)
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
            await operation(_plcService).ConfigureAwait(false);
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

    private async Task<TResult> ExecuteCoreAsync<TResult>(string operationName, Func<IPlcService, Task<TResult>> operation, CancellationToken cancellationToken)
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
            return await operation(_plcService).ConfigureAwait(false);
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