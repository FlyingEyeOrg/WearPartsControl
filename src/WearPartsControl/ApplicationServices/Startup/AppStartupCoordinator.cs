using WearPartsControl.ApplicationServices.AppSettings;
using WearPartsControl.ApplicationServices.PlcService;
using WearPartsControl.Infrastructure.EntityFrameworkCore;

namespace WearPartsControl.ApplicationServices.Startup;

public sealed class AppStartupCoordinator : IAppStartupCoordinator
{
    private readonly IDatabaseInitializer _databaseInitializer;
    private readonly IAppSettingsService _appSettingsService;
    private readonly IPlcStartupConnectionService _plcStartupConnectionService;
    private readonly object _syncRoot = new();
    private Task? _initializationTask;

    public AppStartupCoordinator(
        IDatabaseInitializer databaseInitializer,
        IAppSettingsService appSettingsService,
        IPlcStartupConnectionService plcStartupConnectionService)
    {
        _databaseInitializer = databaseInitializer;
        _appSettingsService = appSettingsService;
        _plcStartupConnectionService = plcStartupConnectionService;
    }

    public Task EnsureInitializedAsync(CancellationToken cancellationToken = default)
    {
        var initializationTask = GetOrCreateInitializationTask();
        return cancellationToken.CanBeCanceled
            ? WaitAsync(initializationTask, cancellationToken)
            : initializationTask;
    }

    private Task GetOrCreateInitializationTask()
    {
        if (_initializationTask is not null)
        {
            return _initializationTask;
        }

        lock (_syncRoot)
        {
            _initializationTask ??= InitializeCoreAsync();
            return _initializationTask;
        }
    }

    private async Task InitializeCoreAsync()
    {
        await _databaseInitializer.InitializeAsync().ConfigureAwait(false);

        var plcConnectionResult = await _plcStartupConnectionService.EnsureConnectedAsync().ConfigureAwait(false);
        if (plcConnectionResult.Status == PlcStartupConnectionStatus.Connected)
        {
            return;
        }

        var settings = await _appSettingsService.GetAsync().ConfigureAwait(false);
        if (!settings.IsWearPartMonitoringEnabled)
        {
            return;
        }

        settings.IsWearPartMonitoringEnabled = false;
        await _appSettingsService.SaveAsync(settings).ConfigureAwait(false);
    }

    private static async Task WaitAsync(Task initializationTask, CancellationToken cancellationToken)
    {
        if (initializationTask.IsCompleted)
        {
            await initializationTask.ConfigureAwait(false);
            return;
        }

        var cancellationTask = Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        var completedTask = await Task.WhenAny(initializationTask, cancellationTask).ConfigureAwait(false);
        if (completedTask == cancellationTask)
        {
            cancellationToken.ThrowIfCancellationRequested();
        }

        await initializationTask.ConfigureAwait(false);
    }
}