using WearPartsControl.ApplicationServices.AppSettings;
using WearPartsControl.ApplicationServices.ClientAppInfo;
using Serilog;

namespace WearPartsControl.ApplicationServices.PlcService;

public sealed class PlcStartupConnectionService : IPlcStartupConnectionService
{
    private readonly IAppSettingsService _appSettingsService;
    private readonly IClientAppInfoService _clientAppInfoService;
    private readonly IPlcService _plcService;

    public PlcStartupConnectionService(
        IAppSettingsService appSettingsService,
        IClientAppInfoService clientAppInfoService,
        IPlcService plcService)
    {
        _appSettingsService = appSettingsService;
        _clientAppInfoService = clientAppInfoService;
        _plcService = plcService;
    }

    public async Task<PlcStartupConnectionResult> EnsureConnectedAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _appSettingsService.GetAsync(cancellationToken).ConfigureAwait(false);
        if (!settings.IsSetClientAppInfo || string.IsNullOrWhiteSpace(settings.ResourceNumber))
        {
            return PlcStartupConnectionResult.NotConfigured();
        }

        var clientAppInfo = await _clientAppInfoService.GetAsync(cancellationToken).ConfigureAwait(false);
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