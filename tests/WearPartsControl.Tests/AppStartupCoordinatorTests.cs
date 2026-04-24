using System.Threading;
using WearPartsControl.ApplicationServices.AppSettings;
using WearPartsControl.ApplicationServices.PlcService;
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
        var coordinator = new AppStartupCoordinator(initializer, new StubAppSettingsService(), new StubPlcStartupConnectionService());

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
        var coordinator = new AppStartupCoordinator(initializer, new StubAppSettingsService(), new StubPlcStartupConnectionService());
        using var cancellationTokenSource = new CancellationTokenSource();

        var sharedInitializationTask = coordinator.EnsureInitializedAsync();
        var canceledWaitTask = coordinator.EnsureInitializedAsync(cancellationToken: cancellationTokenSource.Token);
        cancellationTokenSource.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => canceledWaitTask);

        initializer.PendingTaskSource.SetResult();
        await sharedInitializationTask;
        Assert.Equal(1, initializer.CallCount);
    }

    [Fact]
    public async Task EnsureInitializedAsync_WhenPlcConnectionFails_ShouldDisableWearPartMonitoring()
    {
        var initializer = new StubDatabaseInitializer();
        var appSettingsService = new StubAppSettingsService
        {
            Current = new AppSettings
            {
                IsWearPartMonitoringEnabled = true,
                IsSetClientAppInfo = true,
                ResourceNumber = "RES-01"
            }
        };
        var plcStartupConnectionService = new StubPlcStartupConnectionService
        {
            Result = PlcStartupConnectionResult.Failed("PLC 连接失败")
        };
        var coordinator = new AppStartupCoordinator(initializer, appSettingsService, plcStartupConnectionService);

        await coordinator.EnsureInitializedAsync();

        Assert.False(appSettingsService.Current.IsWearPartMonitoringEnabled);
        Assert.Equal(1, appSettingsService.SaveCallCount);
        Assert.Equal(1, plcStartupConnectionService.CallCount);
    }

    [Fact]
    public async Task EnsureInitializedAsync_WhenWearPartMonitoringDisabled_ShouldNotConnectPlc()
    {
        var initializer = new StubDatabaseInitializer();
        var appSettingsService = new StubAppSettingsService
        {
            Current = new AppSettings
            {
                IsWearPartMonitoringEnabled = false,
                IsSetClientAppInfo = true,
                ResourceNumber = "RES-01"
            }
        };
        var plcStartupConnectionService = new StubPlcStartupConnectionService();
        var coordinator = new AppStartupCoordinator(initializer, appSettingsService, plcStartupConnectionService);

        await coordinator.EnsureInitializedAsync();

        Assert.Equal(0, plcStartupConnectionService.CallCount);
        Assert.Equal(0, appSettingsService.SaveCallCount);
    }

    [Fact]
    public async Task EnsureInitializedAsync_WhenInitializationPreviouslyFaulted_ShouldRetry()
    {
        var initializer = new StubDatabaseInitializer
        {
            FailuresRemaining = 1
        };
        var coordinator = new AppStartupCoordinator(initializer, new StubAppSettingsService(), new StubPlcStartupConnectionService());

        await Assert.ThrowsAsync<InvalidOperationException>(() => coordinator.EnsureInitializedAsync());

        await coordinator.EnsureInitializedAsync();

        Assert.Equal(2, initializer.CallCount);
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

    private sealed class StubAppSettingsService : IAppSettingsService
    {
        public AppSettings Current { get; set; } = new();

        public int SaveCallCount { get; private set; }

        public event EventHandler<AppSettings>? SettingsSaved;

        public ValueTask<AppSettings> GetAsync(CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(Current);
        }

        public ValueTask SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
        {
            Current = settings;
            SaveCallCount++;
            SettingsSaved?.Invoke(this, settings);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class StubPlcStartupConnectionService : IPlcStartupConnectionService
    {
        public int CallCount { get; private set; }

        public PlcStartupConnectionResult Result { get; set; } = PlcStartupConnectionResult.Connected();

        public Task<PlcStartupConnectionResult> EnsureConnectedAsync(CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(Result);
        }
    }
}