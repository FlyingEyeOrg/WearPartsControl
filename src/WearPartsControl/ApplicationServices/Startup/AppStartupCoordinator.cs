using WearPartsControl.ApplicationServices.Localization;
using WearPartsControl.Infrastructure.EntityFrameworkCore;

namespace WearPartsControl.ApplicationServices.Startup;

public sealed class AppStartupCoordinator : IAppStartupCoordinator
{
    private readonly IDatabaseInitializer _databaseInitializer;
    private readonly IStartupPlcWarmupService _startupPlcWarmupService;
    private readonly object _syncRoot = new();
    private Task? _initializationTask;

    public AppStartupCoordinator(
        IDatabaseInitializer databaseInitializer,
        IStartupPlcWarmupService startupPlcWarmupService)
    {
        _databaseInitializer = databaseInitializer;
        _startupPlcWarmupService = startupPlcWarmupService;
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
        var initializationTask = _initializationTask;
        if (CanReuseInitializationTask(initializationTask))
        {
            return initializationTask!;
        }

        lock (_syncRoot)
        {
            if (!CanReuseInitializationTask(_initializationTask))
            {
                _initializationTask = InitializeCoreAsync(reportLoadingAsync);
            }

            return _initializationTask!;
        }
    }

    private static bool CanReuseInitializationTask(Task? initializationTask)
    {
        return initializationTask is not null
            && !initializationTask.IsFaulted
            && !initializationTask.IsCanceled;
    }

    private async Task InitializeCoreAsync(Func<string, Task>? reportLoadingAsync)
    {
        await ReportLoadingAsync(reportLoadingAsync, LocalizedText.Get("ViewModels.MainWindowVm.InitializingDatabase")).ConfigureAwait(false);
        await _databaseInitializer.InitializeAsync().ConfigureAwait(false);
        await _startupPlcWarmupService.WarmupAsync(reportLoadingAsync).ConfigureAwait(false);
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