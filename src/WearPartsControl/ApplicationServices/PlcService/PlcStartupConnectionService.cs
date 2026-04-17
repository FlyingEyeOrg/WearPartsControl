using WearPartsControl.ApplicationServices.AppSettings;
using WearPartsControl.ApplicationServices.ClientAppInfo;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace WearPartsControl.ApplicationServices.PlcService;

public sealed class PlcStartupConnectionService : IPlcStartupConnectionService
{
    private readonly IAppSettingsService _appSettingsService;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly IPlcService _plcService;

    public PlcStartupConnectionService(
        IAppSettingsService appSettingsService,
        IServiceScopeFactory serviceScopeFactory,
        IPlcService plcService)
    {
        _appSettingsService = appSettingsService;
        _serviceScopeFactory = serviceScopeFactory;
        _plcService = plcService;
    }

    public async Task<PlcStartupConnectionResult> EnsureConnectedAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _appSettingsService.GetAsync(cancellationToken).ConfigureAwait(false);
        if (!settings.IsSetClientAppInfo || string.IsNullOrWhiteSpace(settings.ResourceNumber))
        {
            return PlcStartupConnectionResult.NotConfigured();
        }

        await using var scope = _serviceScopeFactory.CreateAsyncScope();
        var clientAppInfoService = scope.ServiceProvider.GetRequiredService<IClientAppInfoService>();
        var clientAppInfo = await clientAppInfoService.GetAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(clientAppInfo.ResourceNumber)
            || string.IsNullOrWhiteSpace(clientAppInfo.PlcProtocolType)
            || string.IsNullOrWhiteSpace(clientAppInfo.PlcIpAddress))
        {
            return PlcStartupConnectionResult.NotConfigured();
        }

        try
        {
            var connectionOptions = PlcConnectionOptionsFactory.Create(clientAppInfo);
            await Task.Run(() => _plcService.Connect(connectionOptions), cancellationToken).ConfigureAwait(false);
            return PlcStartupConnectionResult.Connected();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Log.Warning(ex, "Failed to connect PLC during startup for resource {ResourceNumber}", clientAppInfo.ResourceNumber);
            return PlcStartupConnectionResult.Failed($"连接失败：{ex.Message}");
        }
    }
}