using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WearPartsControl.ApplicationServices.Localization;
using WearPartsControl.ApplicationServices.MonitoringLogs;
using WearPartsControl.ApplicationServices.PlcService;
using WearPartsControl.Exceptions;

namespace WearPartsControl.ApplicationServices.PartServices;

public sealed class WearPartMonitoringHostedService : BackgroundService
{
    private static readonly TimeSpan MonitorInterval = TimeSpan.FromMinutes(5);

    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly IMonitoringRuntimeStateProvider _monitoringRuntimeStateProvider;
    private readonly ILogger<WearPartMonitoringHostedService> _logger;
    private readonly IWearPartMonitoringLogPipeline? _monitoringLogPipeline;

    public WearPartMonitoringHostedService(
        IServiceScopeFactory serviceScopeFactory,
        IMonitoringRuntimeStateProvider monitoringRuntimeStateProvider,
        ILogger<WearPartMonitoringHostedService> logger,
        IWearPartMonitoringLogPipeline? monitoringLogPipeline = null)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _monitoringRuntimeStateProvider = monitoringRuntimeStateProvider;
        _logger = logger;
        _monitoringLogPipeline = monitoringLogPipeline;
    }

    public Task RunOnceAsync(CancellationToken cancellationToken = default)
    {
        return ExecuteCycleAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await ExecuteCycleAsync(stoppingToken).ConfigureAwait(false);

        using var timer = new PeriodicTimer(MonitorInterval);
        while (!stoppingToken.IsCancellationRequested
            && await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            await ExecuteCycleAsync(stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task ExecuteCycleAsync(CancellationToken cancellationToken)
    {
        try
        {
            var runtimeState = await _monitoringRuntimeStateProvider.GetCurrentAsync(cancellationToken).ConfigureAwait(false);

            if (!runtimeState.IsClientAppInfoConfigured)
            {
                _logger.LogDebug("跳过后台易损件监控：当前未设置客户端基础信息。");
                PublishServiceLog(WearPartMonitoringLogLevel.Information, LocalizedText.Get("Services.WearPartMonitoringLog.MonitorSkippedClientAppMissing"));
                return;
            }

            if (!runtimeState.HasResourceNumber)
            {
                _logger.LogDebug("跳过后台易损件监控：当前未配置资源号。");
                PublishServiceLog(WearPartMonitoringLogLevel.Information, LocalizedText.Get("Services.WearPartMonitoringLog.MonitorSkippedResourceNumberMissing"));
                return;
            }

            if (!runtimeState.IsWearPartMonitoringEnabled)
            {
                _logger.LogDebug("跳过后台易损件监控：当前已关闭后台监控。");
                PublishServiceLog(WearPartMonitoringLogLevel.Information, LocalizedText.Get("Services.WearPartMonitoringLog.MonitorSkippedDisabled"), runtimeState.ResourceNumber);
                return;
            }

            PublishServiceLog(WearPartMonitoringLogLevel.Information, LocalizedText.Format("Services.WearPartMonitoringLog.MonitorCycleStarted", runtimeState.ResourceNumber), runtimeState.ResourceNumber);

            await using var scope = _serviceScopeFactory.CreateAsyncScope();
            var plcStartupConnectionService = scope.ServiceProvider.GetRequiredService<IPlcStartupConnectionService>();
            var monitorService = scope.ServiceProvider.GetRequiredService<IWearPartMonitorService>();

            var plcConnectionResult = await plcStartupConnectionService.EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);
            if (plcConnectionResult.Status != PlcStartupConnectionStatus.Connected)
            {
                _logger.LogWarning("跳过后台易损件监控：PLC 未连接，原因：{Message}", plcConnectionResult.Message);
                PublishServiceLog(WearPartMonitoringLogLevel.Warning, LocalizedText.Format("Services.WearPartMonitoringLog.MonitorSkippedPlcNotConnected", plcConnectionResult.Message), runtimeState.ResourceNumber);
                return;
            }

            var results = await monitorService.MonitorByResourceNumberAsync(runtimeState.ResourceNumber, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("后台易损件监控完成，资源号 {ResourceNumber}，处理 {Count} 项。", runtimeState.ResourceNumber, results.Count);
            PublishServiceLog(WearPartMonitoringLogLevel.Information, LocalizedText.Format("Services.WearPartMonitoringLog.MonitorCycleCompleted", runtimeState.ResourceNumber, results.Count), runtimeState.ResourceNumber);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (EntityNotFoundException ex)
        {
            _logger.LogWarning(ex, "后台易损件监控跳过：{Message}", ex.Message);
            PublishServiceLog(WearPartMonitoringLogLevel.Warning, LocalizedText.Format("Services.WearPartMonitoringLog.MonitorSkippedKnownError", ex.Message), exception: ex);
        }
        catch (UserFriendlyException ex)
        {
            _logger.LogWarning(ex, "后台易损件监控失败：{Message}", ex.Message);
            PublishServiceLog(WearPartMonitoringLogLevel.Warning, LocalizedText.Format("Services.WearPartMonitoringLog.MonitorFailed", ex.Message), exception: ex);
        }
        catch (BusinessException ex)
        {
            _logger.LogWarning(ex, "后台易损件监控失败：{Message}", ex.Message);
            PublishServiceLog(WearPartMonitoringLogLevel.Warning, LocalizedText.Format("Services.WearPartMonitoringLog.MonitorFailed", ex.Message), exception: ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "后台易损件监控发生未处理异常。");
            PublishServiceLog(WearPartMonitoringLogLevel.Error, LocalizedText.Format("Services.WearPartMonitoringLog.MonitorUnhandled", ex.Message), exception: ex);
        }
    }

    private void PublishServiceLog(WearPartMonitoringLogLevel level, string message, string? resourceNumber = null, Exception? exception = null)
    {
        _monitoringLogPipeline?.Publish(
            level,
            WearPartMonitoringLogCategory.Service,
            message,
            operationName: nameof(WearPartMonitoringHostedService),
            resourceNumber: resourceNumber,
            details: exception?.Message,
            exception: exception);
    }
}