using System.Diagnostics;
using System.Threading;
using Microsoft.Extensions.Logging;
using WearPartsControl.ApplicationServices.Localization;
using WearPartsControl.ApplicationServices.MonitoringLogs;
using AppSettingsModel = WearPartsControl.ApplicationServices.AppSettings.AppSettings;
using IAppSettingsService = WearPartsControl.ApplicationServices.AppSettings.IAppSettingsService;
using PlcPipelineSettings = WearPartsControl.ApplicationServices.AppSettings.PlcPipelineSettings;

namespace WearPartsControl.ApplicationServices.PlcService;

public sealed class PlcOperationPipeline : IPlcOperationPipeline
{
    private const int DefaultSlowQueueWaitThresholdMilliseconds = 100;
    private const int DefaultSlowExecutionThresholdMilliseconds = 500;

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly IPlcService _plcService;
    private readonly ILogger<PlcOperationPipeline> _logger;
    private readonly IAppSettingsService? _appSettingsService;
    private readonly IWearPartMonitoringLogPipeline? _monitoringLogPipeline;
    private int _pendingOperations;
    private long _operationSequence;
    private int _slowQueueWaitThresholdMilliseconds = DefaultSlowQueueWaitThresholdMilliseconds;
    private int _slowExecutionThresholdMilliseconds = DefaultSlowExecutionThresholdMilliseconds;

    internal PlcOperationPipeline(
        IPlcService plcService,
        ILogger<PlcOperationPipeline> logger,
        IAppSettingsService? appSettingsService = null,
        IWearPartMonitoringLogPipeline? monitoringLogPipeline = null)
    {
        _plcService = plcService;
        _logger = logger;
        _appSettingsService = appSettingsService;
        _monitoringLogPipeline = monitoringLogPipeline;

        if (_appSettingsService is not null)
        {
            TryLoadThresholdsFromSettings();
            _appSettingsService.SettingsSaved += OnSettingsSaved;
        }
    }

    public Task ConnectAsync(string operationName, PlcConnectionOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        return ExecuteCoreAsync(
            operationName,
            plcService => plcService.ConnectAsync(options, forceReconnect: false, cancellationToken),
            cancellationToken,
            options.IpAddress,
            FormatConnectionDetails(options));
    }

    public Task ForceReconnectAsync(string operationName, PlcConnectionOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        return ExecuteCoreAsync(
            operationName,
            plcService => plcService.ConnectAsync(options, forceReconnect: true, cancellationToken),
            cancellationToken,
            options.IpAddress,
            FormatConnectionDetails(options));
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
        return ExecuteCoreAsync(operationName, plcService => Task.FromResult(plcService.Read<TValue>(address, retryCount)), cancellationToken, address);
    }

    public Task WriteAsync<TValue>(string operationName, string address, TValue value, int retryCount = 1, CancellationToken cancellationToken = default)
    {
        return ExecuteCoreAsync(operationName, plcService =>
        {
            plcService.Write(address, value, retryCount);
            return Task.CompletedTask;
        }, cancellationToken, address, FormatWriteDetails(value));
    }

    private async Task ExecuteCoreAsync(
        string operationName,
        Func<IPlcService, Task> operation,
        CancellationToken cancellationToken,
        string? address = null,
        string? details = null)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ValidateOperationName(operationName);

        var operationId = Interlocked.Increment(ref _operationSequence);
        var queuedAt = Stopwatch.GetTimestamp();
        var queueDepth = Interlocked.Increment(ref _pendingOperations);

        _logger.LogDebug(LocalizedText.Get("Services.PlcPipeline.LogOperationQueued"), operationName, operationId, queueDepth);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        var waitElapsed = Stopwatch.GetElapsedTime(queuedAt);
        var executionStartedAt = Stopwatch.GetTimestamp();

        LogQueueWait(operationName, operationId, queueDepth, waitElapsed);

        try
        {
            await Task.Run(() => operation(_plcService), cancellationToken).ConfigureAwait(false);
            PublishPlcOperationCompleted(operationName, address, details, Stopwatch.GetElapsedTime(executionStartedAt));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var executionElapsed = Stopwatch.GetElapsedTime(executionStartedAt);
            _logger.LogWarning(ex, LocalizedText.Get("Services.PlcPipeline.LogOperationFailed"), operationName, operationId, executionElapsed.TotalMilliseconds);
            PublishPlcOperationFailed(operationName, address, details, executionElapsed, ex);
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

    private async Task<TResult> ExecuteCoreAsync<TResult>(
        string operationName,
        Func<IPlcService, Task<TResult>> operation,
        CancellationToken cancellationToken,
        string? address = null,
        string? details = null)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ValidateOperationName(operationName);

        var operationId = Interlocked.Increment(ref _operationSequence);
        var queuedAt = Stopwatch.GetTimestamp();
        var queueDepth = Interlocked.Increment(ref _pendingOperations);

        _logger.LogDebug(LocalizedText.Get("Services.PlcPipeline.LogOperationQueued"), operationName, operationId, queueDepth);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        var waitElapsed = Stopwatch.GetElapsedTime(queuedAt);
        var executionStartedAt = Stopwatch.GetTimestamp();

        LogQueueWait(operationName, operationId, queueDepth, waitElapsed);

        try
        {
            var result = await Task.Run(() => operation(_plcService), cancellationToken).ConfigureAwait(false);
            PublishPlcOperationCompleted(operationName, address, details, Stopwatch.GetElapsedTime(executionStartedAt));
            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var executionElapsed = Stopwatch.GetElapsedTime(executionStartedAt);
            _logger.LogWarning(ex, LocalizedText.Get("Services.PlcPipeline.LogOperationFailed"), operationName, operationId, executionElapsed.TotalMilliseconds);
            PublishPlcOperationFailed(operationName, address, details, executionElapsed, ex);
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
        if (waitElapsed.TotalMilliseconds >= Volatile.Read(ref _slowQueueWaitThresholdMilliseconds))
        {
            _logger.LogWarning(LocalizedText.Get("Services.PlcPipeline.LogQueueWaitSlow"), operationName, operationId, waitElapsed.TotalMilliseconds, queueDepth);
            return;
        }

        _logger.LogDebug(LocalizedText.Get("Services.PlcPipeline.LogQueueWaitAcquired"), operationName, operationId, waitElapsed.TotalMilliseconds);
    }

    private void LogExecution(string operationName, long operationId, TimeSpan executionElapsed)
    {
        if (executionElapsed.TotalMilliseconds >= Volatile.Read(ref _slowExecutionThresholdMilliseconds))
        {
            _logger.LogWarning(LocalizedText.Get("Services.PlcPipeline.LogExecutionSlow"), operationName, operationId, executionElapsed.TotalMilliseconds);
            return;
        }

        _logger.LogDebug(LocalizedText.Get("Services.PlcPipeline.LogExecutionCompleted"), operationName, operationId, executionElapsed.TotalMilliseconds);
    }

    private static void ValidateOperationName(string operationName)
    {
        if (string.IsNullOrWhiteSpace(operationName))
        {
            throw new ArgumentException(LocalizedText.Get("Services.PlcPipeline.OperationNameRequired"), nameof(operationName));
        }
    }

    private void OnSettingsSaved(object? sender, AppSettingsModel settings)
    {
        ApplyThresholds(settings);
    }

    private void TryLoadThresholdsFromSettings()
    {
        try
        {
            var settings = _appSettingsService!.GetAsync().AsTask().GetAwaiter().GetResult();
            ApplyThresholds(settings);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, LocalizedText.Get("Services.PlcPipeline.LogLoadThresholdsFailed"));
        }
    }

    private void ApplyThresholds(AppSettingsModel settings)
    {
        var plcPipeline = settings.PlcPipeline ?? new PlcPipelineSettings();
        Interlocked.Exchange(ref _slowQueueWaitThresholdMilliseconds, plcPipeline.SlowQueueWaitThresholdMilliseconds <= 0 ? DefaultSlowQueueWaitThresholdMilliseconds : plcPipeline.SlowQueueWaitThresholdMilliseconds);
        Interlocked.Exchange(ref _slowExecutionThresholdMilliseconds, plcPipeline.SlowExecutionThresholdMilliseconds <= 0 ? DefaultSlowExecutionThresholdMilliseconds : plcPipeline.SlowExecutionThresholdMilliseconds);
    }

    private void PublishPlcOperationCompleted(string operationName, string? address, string? details, TimeSpan executionElapsed)
    {
        if (!ShouldPublishMonitoringOperation(operationName) || _monitoringLogPipeline is null)
        {
            return;
        }

        var isSlow = executionElapsed.TotalMilliseconds >= Volatile.Read(ref _slowExecutionThresholdMilliseconds);
        _monitoringLogPipeline.Publish(
            isSlow ? WearPartMonitoringLogLevel.Warning : WearPartMonitoringLogLevel.Information,
            WearPartMonitoringLogCategory.Plc,
            LocalizedText.Format(
                isSlow ? "Services.WearPartMonitoringLog.PlcOperationSlow" : "Services.WearPartMonitoringLog.PlcOperationSucceeded",
                operationName,
                executionElapsed.TotalMilliseconds),
            operationName,
            address: address,
            details: details);
    }

    private void PublishPlcOperationFailed(string operationName, string? address, string? details, TimeSpan executionElapsed, Exception exception)
    {
        if (!ShouldPublishMonitoringOperation(operationName) || _monitoringLogPipeline is null)
        {
            return;
        }

        _monitoringLogPipeline.Publish(
            WearPartMonitoringLogLevel.Error,
            WearPartMonitoringLogCategory.Plc,
            LocalizedText.Format("Services.WearPartMonitoringLog.PlcOperationFailed", operationName, executionElapsed.TotalMilliseconds, exception.Message),
            operationName,
            address: address,
            details: details,
            exception: exception);
    }

    private static bool ShouldPublishMonitoringOperation(string operationName)
    {
        return operationName.StartsWith("Monitor/", StringComparison.Ordinal);
    }

    private static string FormatConnectionDetails(PlcConnectionOptions options)
    {
        return $"{options.PlcType} {options.IpAddress}:{options.Port}";
    }

    private static string? FormatWriteDetails<TValue>(TValue value)
    {
        return value is null ? null : Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);
    }
}