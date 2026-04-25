using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WearPartsControl.ApplicationServices.PlcService;
using WearPartsControl.Exceptions;

namespace WearPartsControl.ApplicationServices.PartServices;

public sealed class WearPartMonitoringHostedService : BackgroundService
{
    private static readonly TimeSpan MonitorInterval = TimeSpan.FromMinutes(5);

    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly IMonitoringRuntimeStateProvider _monitoringRuntimeStateProvider;
    private readonly ILogger<WearPartMonitoringHostedService> _logger;

    public WearPartMonitoringHostedService(
        IServiceScopeFactory serviceScopeFactory,
        IMonitoringRuntimeStateProvider monitoringRuntimeStateProvider,
        ILogger<WearPartMonitoringHostedService> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _monitoringRuntimeStateProvider = monitoringRuntimeStateProvider;
        _logger = logger;
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
                return;
            }

            if (!runtimeState.HasResourceNumber)
            {
                _logger.LogDebug("跳过后台易损件监控：当前未配置资源号。");
                return;
            }

            if (!runtimeState.IsWearPartMonitoringEnabled)
            {
                _logger.LogDebug("跳过后台易损件监控：当前已关闭后台监控。");
                return;
            }

            await using var scope = _serviceScopeFactory.CreateAsyncScope();
            var plcStartupConnectionService = scope.ServiceProvider.GetRequiredService<IPlcStartupConnectionService>();
            var monitorService = scope.ServiceProvider.GetRequiredService<IWearPartMonitorService>();

            var plcConnectionResult = await plcStartupConnectionService.EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);
            if (plcConnectionResult.Status != PlcStartupConnectionStatus.Connected)
            {
                _logger.LogWarning("跳过后台易损件监控：PLC 未连接，原因：{Message}", plcConnectionResult.Message);
                return;
            }

            var results = await monitorService.MonitorByResourceNumberAsync(runtimeState.ResourceNumber, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("后台易损件监控完成，资源号 {ResourceNumber}，处理 {Count} 项。", runtimeState.ResourceNumber, results.Count);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (EntityNotFoundException ex)
        {
            _logger.LogWarning(ex, "后台易损件监控跳过：{Message}", ex.Message);
        }
        catch (UserFriendlyException ex)
        {
            _logger.LogWarning(ex, "后台易损件监控失败：{Message}", ex.Message);
        }
        catch (BusinessException ex)
        {
            _logger.LogWarning(ex, "后台易损件监控失败：{Message}", ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "后台易损件监控发生未处理异常。");
        }
    }
}