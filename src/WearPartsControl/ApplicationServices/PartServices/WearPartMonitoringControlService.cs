using WearPartsControl.ApplicationServices.AppSettings;
using WearPartsControl.ApplicationServices.PlcService;
using WearPartsControl.Exceptions;

namespace WearPartsControl.ApplicationServices.PartServices;

public sealed class WearPartMonitoringControlService : IWearPartMonitoringControlService
{
    private readonly IMonitoringRuntimeStateProvider _monitoringRuntimeStateProvider;
    private readonly IAppSettingsService _appSettingsService;
    private readonly IPlcStartupConnectionService _plcStartupConnectionService;
    private readonly WearPartMonitoringHostedService _wearPartMonitoringHostedService;

    public WearPartMonitoringControlService(
        IMonitoringRuntimeStateProvider monitoringRuntimeStateProvider,
        IAppSettingsService appSettingsService,
        IPlcStartupConnectionService plcStartupConnectionService,
        WearPartMonitoringHostedService wearPartMonitoringHostedService)
    {
        _monitoringRuntimeStateProvider = monitoringRuntimeStateProvider;
        _appSettingsService = appSettingsService;
        _plcStartupConnectionService = plcStartupConnectionService;
        _wearPartMonitoringHostedService = wearPartMonitoringHostedService;
    }

    public async Task<bool> GetIsEnabledAsync(CancellationToken cancellationToken = default)
    {
        var runtimeState = await _monitoringRuntimeStateProvider.GetCurrentAsync(cancellationToken).ConfigureAwait(false);
        return runtimeState.IsWearPartMonitoringEnabled;
    }

    public async Task EnableAsync(CancellationToken cancellationToken = default)
    {
        var plcConnectionResult = await _plcStartupConnectionService.EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);
        if (plcConnectionResult.Status != PlcStartupConnectionStatus.Connected)
        {
            throw new UserFriendlyException(plcConnectionResult.Message);
        }

        var settings = await _appSettingsService.GetAsync(cancellationToken).ConfigureAwait(false);
        if (!settings.IsWearPartMonitoringEnabled)
        {
            settings.IsWearPartMonitoringEnabled = true;
            await _appSettingsService.SaveAsync(settings, cancellationToken).ConfigureAwait(false);
        }

        await _wearPartMonitoringHostedService.RunOnceAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DisableAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _appSettingsService.GetAsync(cancellationToken).ConfigureAwait(false);
        if (!settings.IsWearPartMonitoringEnabled)
        {
            return;
        }

        settings.IsWearPartMonitoringEnabled = false;
        await _appSettingsService.SaveAsync(settings, cancellationToken).ConfigureAwait(false);
    }
}