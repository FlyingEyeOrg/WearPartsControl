using WearPartsControl.ApplicationServices.AppSettings;
using WearPartsControl.ApplicationServices.PartServices;
using WearPartsControl.ApplicationServices.PlcService;
using WearPartsControl.ApplicationServices.Startup;
using Xunit;

namespace WearPartsControl.Tests;

public sealed class StartupPlcWarmupServiceTests
{
    [Fact]
    public async Task WarmupAsync_WhenMonitoringCannotStart_ShouldSkipPlcConnection()
    {
        var plcStartupConnectionService = new StubPlcStartupConnectionService();
        var service = new StartupPlcWarmupService(
            new MonitoringRuntimeStateProvider(new StubAppSettingsService
            {
                Current = new AppSettings
                {
                    IsSetClientAppInfo = true,
                    ResourceNumber = "RES-01",
                    IsWearPartMonitoringEnabled = false
                }
            }),
            plcStartupConnectionService);

        await service.WarmupAsync();

        Assert.Equal(0, plcStartupConnectionService.CallCount);
    }

    [Fact]
    public async Task WarmupAsync_WhenMonitoringCanStart_ShouldConnectPlcAndReportProgress()
    {
        var plcStartupConnectionService = new StubPlcStartupConnectionService();
        var loadingMessages = new List<string>();
        var service = new StartupPlcWarmupService(
            new MonitoringRuntimeStateProvider(new StubAppSettingsService
            {
                Current = new AppSettings
                {
                    IsSetClientAppInfo = true,
                    ResourceNumber = "RES-01",
                    IsWearPartMonitoringEnabled = true
                }
            }),
            plcStartupConnectionService);

        await service.WarmupAsync(message =>
        {
            loadingMessages.Add(message);
            return Task.CompletedTask;
        });

        Assert.Equal(1, plcStartupConnectionService.CallCount);
        Assert.Single(loadingMessages);
    }

    private sealed class StubAppSettingsService : IAppSettingsService
    {
        public AppSettings Current { get; set; } = new();

        public event EventHandler<AppSettings>? SettingsSaved;

        public ValueTask<AppSettings> GetAsync(CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(Current);
        }

        public ValueTask SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
        {
            Current = settings;
            SettingsSaved?.Invoke(this, settings);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class StubPlcStartupConnectionService : IPlcStartupConnectionService
    {
        public int CallCount { get; private set; }

        public Task<PlcStartupConnectionResult> EnsureConnectedAsync(CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(PlcStartupConnectionResult.Connected());
        }
    }
}