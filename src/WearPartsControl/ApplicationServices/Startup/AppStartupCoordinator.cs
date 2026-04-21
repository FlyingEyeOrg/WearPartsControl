using WearPartsControl.Infrastructure.EntityFrameworkCore;

namespace WearPartsControl.ApplicationServices.Startup;

public sealed class AppStartupCoordinator : IAppStartupCoordinator
{
    private readonly IDatabaseInitializer _databaseInitializer;
    private readonly object _syncRoot = new();
    private Task? _initializationTask;

    public AppStartupCoordinator(IDatabaseInitializer databaseInitializer)
    {
        _databaseInitializer = databaseInitializer;
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