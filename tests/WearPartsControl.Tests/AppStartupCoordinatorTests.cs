using System.Threading;
using WearPartsControl.ApplicationServices.Startup;
using WearPartsControl.Infrastructure.EntityFrameworkCore;
using Xunit;

namespace WearPartsControl.Tests;

public sealed class AppStartupCoordinatorTests
{
    [Fact]
    public async Task EnsureInitializedAsync_WhenCalledMultipleTimes_ShouldOnlyInitializeDatabaseOnce()
    {
        var initializer = new StubDatabaseInitializer();
        var coordinator = new AppStartupCoordinator(initializer);

        await Task.WhenAll(
            coordinator.EnsureInitializedAsync(),
            coordinator.EnsureInitializedAsync(),
            coordinator.EnsureInitializedAsync());

        Assert.Equal(1, initializer.CallCount);
    }

    [Fact]
    public async Task EnsureInitializedAsync_WhenWaitingCallIsCancelled_ShouldNotCancelSharedInitialization()
    {
        var initializer = new StubDatabaseInitializer
        {
            PendingTaskSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)
        };
        var coordinator = new AppStartupCoordinator(initializer);
        using var cancellationTokenSource = new CancellationTokenSource();

        var sharedInitializationTask = coordinator.EnsureInitializedAsync();
        var canceledWaitTask = coordinator.EnsureInitializedAsync(cancellationTokenSource.Token);
        cancellationTokenSource.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => canceledWaitTask);

        initializer.PendingTaskSource.SetResult();
        await sharedInitializationTask;
        Assert.Equal(1, initializer.CallCount);
    }

    private sealed class StubDatabaseInitializer : IDatabaseInitializer
    {
        public int CallCount { get; private set; }

        public TaskCompletionSource? PendingTaskSource { get; set; }

        public Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            CallCount++;

            if (PendingTaskSource is not null)
            {
                return PendingTaskSource.Task;
            }

            return Task.CompletedTask;
        }
    }
}