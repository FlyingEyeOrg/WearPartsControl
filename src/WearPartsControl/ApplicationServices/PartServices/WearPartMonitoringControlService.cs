using WearPartsControl.ApplicationServices.AppSettings;

namespace WearPartsControl.ApplicationServices.PartServices;

public sealed class WearPartMonitoringControlService : IWearPartMonitoringControlService
{
    private readonly IAppSettingsService _appSettingsService;
    private readonly WearPartMonitoringHostedService _wearPartMonitoringHostedService;

    public WearPartMonitoringControlService(IAppSettingsService appSettingsService, WearPartMonitoringHostedService wearPartMonitoringHostedService)
    {
        _appSettingsService = appSettingsService;
        _wearPartMonitoringHostedService = wearPartMonitoringHostedService;
    }

    public async Task<bool> GetIsEnabledAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _appSettingsService.GetAsync(cancellationToken).ConfigureAwait(false);
        return settings.IsWearPartMonitoringEnabled;
    }

    public async Task EnableAsync(CancellationToken cancellationToken = default)
    {
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