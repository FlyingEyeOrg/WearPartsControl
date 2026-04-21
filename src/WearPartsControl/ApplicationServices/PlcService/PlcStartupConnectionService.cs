using WearPartsControl.ApplicationServices.AppSettings;
using WearPartsControl.ApplicationServices.ClientAppInfo;
using WearPartsControl.ApplicationServices.Localization;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace WearPartsControl.ApplicationServices.PlcService;

public sealed class PlcStartupConnectionService : IPlcStartupConnectionService
{
    private readonly IAppSettingsService _appSettingsService;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly IPlcService _plcService;
    private readonly IPlcConnectionStatusService _plcConnectionStatusService;

    public PlcStartupConnectionService(
        IAppSettingsService appSettingsService,
        IServiceScopeFactory serviceScopeFactory,
        IPlcService plcService,
        IPlcConnectionStatusService plcConnectionStatusService)
    {
        _appSettingsService = appSettingsService;
        _serviceScopeFactory = serviceScopeFactory;
        _plcService = plcService;
        _plcConnectionStatusService = plcConnectionStatusService;
    }

    public async Task<PlcStartupConnectionResult> EnsureConnectedAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _appSettingsService.GetAsync(cancellationToken).ConfigureAwait(false);
        if (!settings.IsSetClientAppInfo || string.IsNullOrWhiteSpace(settings.ResourceNumber))
        {
            var notConfigured = PlcStartupConnectionResult.NotConfigured();
            _plcConnectionStatusService.Set(notConfigured);
            return notConfigured;
        }

        await using var scope = _serviceScopeFactory.CreateAsyncScope();
        var clientAppInfoService = scope.ServiceProvider.GetRequiredService<IClientAppInfoService>();
        var clientAppInfo = await clientAppInfoService.GetAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(clientAppInfo.ResourceNumber)
            || string.IsNullOrWhiteSpace(clientAppInfo.PlcProtocolType)
            || string.IsNullOrWhiteSpace(clientAppInfo.PlcIpAddress))
        {
            var notConfigured = PlcStartupConnectionResult.NotConfigured();
            _plcConnectionStatusService.Set(notConfigured);
            return notConfigured;
        }

        try
        {
            _plcConnectionStatusService.Set(PlcStartupConnectionResult.Connecting());
            var connectionOptions = PlcConnectionOptionsFactory.Create(clientAppInfo);
            await Task.Run(() => _plcService.Connect(connectionOptions), cancellationToken).ConfigureAwait(false);
            var connected = PlcStartupConnectionResult.Connected();
            _plcConnectionStatusService.Set(connected);
            return connected;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Log.Warning(ex, "Failed to connect PLC during startup for resource {ResourceNumber}", clientAppInfo.ResourceNumber);
            var failed = PlcStartupConnectionResult.Failed(LocalizedText.Format("Services.PlcStartupConnection.ConnectFailed", ex.Message));
            _plcConnectionStatusService.Set(failed);
            return failed;
        }
    }
}