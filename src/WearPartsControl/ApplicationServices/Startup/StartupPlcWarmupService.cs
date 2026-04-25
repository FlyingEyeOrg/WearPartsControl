using WearPartsControl.ApplicationServices.Localization;
using WearPartsControl.ApplicationServices.PartServices;
using WearPartsControl.ApplicationServices.PlcService;

namespace WearPartsControl.ApplicationServices.Startup;

public sealed class StartupPlcWarmupService : IStartupPlcWarmupService
{
    private readonly IMonitoringRuntimeStateProvider _monitoringRuntimeStateProvider;
    private readonly IPlcStartupConnectionService _plcStartupConnectionService;

    public StartupPlcWarmupService(
        IMonitoringRuntimeStateProvider monitoringRuntimeStateProvider,
        IPlcStartupConnectionService plcStartupConnectionService)
    {
        _monitoringRuntimeStateProvider = monitoringRuntimeStateProvider;
        _plcStartupConnectionService = plcStartupConnectionService;
    }

    public async Task WarmupAsync(Func<string, Task>? reportLoadingAsync = null, CancellationToken cancellationToken = default)
    {
        var runtimeState = await _monitoringRuntimeStateProvider.GetCurrentAsync(cancellationToken).ConfigureAwait(false);
        if (!runtimeState.CanWarmUpPlcOnStartup)
        {
            return;
        }

        if (reportLoadingAsync is not null)
        {
            await reportLoadingAsync(LocalizedText.Get("Services.PlcStartupConnection.Connecting")).ConfigureAwait(false);
        }

        await _plcStartupConnectionService.EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);
    }
}