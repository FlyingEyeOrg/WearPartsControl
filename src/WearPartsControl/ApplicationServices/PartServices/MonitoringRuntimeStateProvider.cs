using WearPartsControl.ApplicationServices.AppSettings;

namespace WearPartsControl.ApplicationServices.PartServices;

public sealed class MonitoringRuntimeStateProvider : IMonitoringRuntimeStateProvider
{
    private readonly IAppSettingsService _appSettingsService;

    public MonitoringRuntimeStateProvider(IAppSettingsService appSettingsService)
    {
        _appSettingsService = appSettingsService;
    }

    public async ValueTask<MonitoringRuntimeState> GetCurrentAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _appSettingsService.GetAsync(cancellationToken).ConfigureAwait(false);
        return new MonitoringRuntimeState(
            settings.IsSetClientAppInfo,
            settings.ResourceNumber?.Trim() ?? string.Empty,
            settings.IsWearPartMonitoringEnabled);
    }
}