using WearPartsControl.ApplicationServices.AppSettings;
using WearPartsControl.ApplicationServices.Localization;
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

    public Task EnsureInitializedAsync(Func<string, Task>? reportLoadingAsync = null, CancellationToken cancellationToken = default)
    {
        var initializationTask = GetOrCreateInitializationTask(reportLoadingAsync);
        return cancellationToken.CanBeCanceled
            ? WaitAsync(initializationTask, cancellationToken)
            : initializationTask;
    }

    private Task GetOrCreateInitializationTask(Func<string, Task>? reportLoadingAsync)
    {
        if (_initializationTask is not null)
        {
            return _initializationTask;
        }

        lock (_syncRoot)
        {
            _initializationTask ??= InitializeCoreAsync(reportLoadingAsync);
            return _initializationTask;
        }
    }

    private async Task InitializeCoreAsync(Func<string, Task>? reportLoadingAsync)
    {
        await ReportLoadingAsync(reportLoadingAsync, LocalizedText.Get("ViewModels.MainWindowVm.InitializingDatabase")).ConfigureAwait(false);
        await _databaseInitializer.InitializeAsync().ConfigureAwait(false);

        var settings = await _appSettingsService.GetAsync().ConfigureAwait(false);
        if (!settings.IsSetClientAppInfo || string.IsNullOrWhiteSpace(settings.ResourceNumber) || !settings.IsWearPartMonitoringEnabled)
        {
            return;
        }

        await ReportLoadingAsync(reportLoadingAsync, LocalizedText.Get("Services.PlcStartupConnection.Connecting")).ConfigureAwait(false);
        var plcConnectionResult = await _plcStartupConnectionService.EnsureConnectedAsync().ConfigureAwait(false);
        if (plcConnectionResult.Status == PlcStartupConnectionStatus.Connected)
        {
            return;
        }

        settings.IsWearPartMonitoringEnabled = false;
        await _appSettingsService.SaveAsync(settings).ConfigureAwait(false);
    }

    private static Task ReportLoadingAsync(Func<string, Task>? reportLoadingAsync, string message)
    {
        return reportLoadingAsync is null
            ? Task.CompletedTask
            : reportLoadingAsync(message);
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