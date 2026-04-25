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
        var startupPlcWarmupService = new StubStartupPlcWarmupService();
        var coordinator = new AppStartupCoordinator(initializer, startupPlcWarmupService);

        await Task.WhenAll(
            coordinator.EnsureInitializedAsync(),
            coordinator.EnsureInitializedAsync(),
            coordinator.EnsureInitializedAsync());

        Assert.Equal(1, initializer.CallCount);
        Assert.Equal(1, startupPlcWarmupService.CallCount);
    }

    [Fact]
    public async Task EnsureInitializedAsync_WhenWaitingCallIsCancelled_ShouldNotCancelSharedInitialization()
    {
        var initializer = new StubDatabaseInitializer
        {
            PendingTaskSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)
        };
        var startupPlcWarmupService = new StubStartupPlcWarmupService();
        var coordinator = new AppStartupCoordinator(initializer, startupPlcWarmupService);
        using var cancellationTokenSource = new CancellationTokenSource();

        var sharedInitializationTask = coordinator.EnsureInitializedAsync();
        var canceledWaitTask = coordinator.EnsureInitializedAsync(cancellationToken: cancellationTokenSource.Token);
        cancellationTokenSource.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => canceledWaitTask);

        initializer.PendingTaskSource.SetResult();
        await sharedInitializationTask;
        Assert.Equal(1, initializer.CallCount);
        Assert.Equal(1, startupPlcWarmupService.CallCount);
    }

    [Fact]
    public async Task EnsureInitializedAsync_ShouldDelegateWarmupAfterDatabaseInitialization()
    {
        var initializer = new StubDatabaseInitializer();
        var startupPlcWarmupService = new StubStartupPlcWarmupService();
        var coordinator = new AppStartupCoordinator(initializer, startupPlcWarmupService);

        await coordinator.EnsureInitializedAsync();

        Assert.Equal(1, startupPlcWarmupService.CallCount);
    }

    [Fact]
    public async Task EnsureInitializedAsync_WhenWarmupFails_ShouldSurfaceFailureAndRetry()
    {
        var initializer = new StubDatabaseInitializer();
        var startupPlcWarmupService = new StubStartupPlcWarmupService
        {
            FailuresRemaining = 1
        };
        var coordinator = new AppStartupCoordinator(initializer, startupPlcWarmupService);

        await Assert.ThrowsAsync<InvalidOperationException>(() => coordinator.EnsureInitializedAsync());
        await coordinator.EnsureInitializedAsync();

        Assert.Equal(2, startupPlcWarmupService.CallCount);
    }

    [Fact]
    public async Task EnsureInitializedAsync_WhenInitializationPreviouslyFaulted_ShouldRetry()
    {
        var initializer = new StubDatabaseInitializer
        {
            FailuresRemaining = 1
        };
        var startupPlcWarmupService = new StubStartupPlcWarmupService();
        var coordinator = new AppStartupCoordinator(initializer, startupPlcWarmupService);

        await Assert.ThrowsAsync<InvalidOperationException>(() => coordinator.EnsureInitializedAsync());

        await coordinator.EnsureInitializedAsync();

        Assert.Equal(2, initializer.CallCount);
        Assert.Equal(1, startupPlcWarmupService.CallCount);
    }

    private sealed class StubDatabaseInitializer : IDatabaseInitializer
    {
        public int CallCount { get; private set; }

        public TaskCompletionSource? PendingTaskSource { get; set; }

        public int FailuresRemaining { get; set; }

        public Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            CallCount++;

            if (FailuresRemaining > 0)
            {
                FailuresRemaining--;
                throw new InvalidOperationException("transient init failure");
            }

            if (PendingTaskSource is not null)
            {
                return PendingTaskSource.Task;
            }

            return Task.CompletedTask;
        }
    }

    private sealed class StubStartupPlcWarmupService : IStartupPlcWarmupService
    {
        public int CallCount { get; private set; }

        public int FailuresRemaining { get; set; }

        public Task WarmupAsync(Func<string, Task>? reportLoadingAsync = null, CancellationToken cancellationToken = default)
        {
            CallCount++;

            if (FailuresRemaining > 0)
            {
                FailuresRemaining--;
                throw new InvalidOperationException("warmup failure");
            }

            return Task.CompletedTask;
        }
    }
}