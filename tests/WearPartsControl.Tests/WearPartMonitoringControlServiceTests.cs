using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using WearPartsControl.ApplicationServices.AppSettings;
using WearPartsControl.ApplicationServices.PartServices;
using WearPartsControl.ApplicationServices.PlcService;
using WearPartsControl.Exceptions;
using Xunit;

namespace WearPartsControl.Tests;

public sealed class WearPartMonitoringControlServiceTests
{
    [Fact]
    public async Task EnableAsync_WhenPlcConnectionFails_ShouldThrowAndKeepMonitoringDisabled()
    {
        var appSettingsService = new StubAppSettingsService
        {
            Current = new AppSettings
            {
                ResourceNumber = "RES-01",
                IsWearPartMonitoringEnabled = false
            }
        };
        var hostedService = CreateHostedService(appSettingsService, new TrackableWearPartMonitorService(), PlcStartupConnectionResult.Connected());
        var service = new WearPartMonitoringControlService(
            appSettingsService,
            new StubPlcStartupConnectionService
            {
                Result = PlcStartupConnectionResult.Failed("PLC 连接失败")
            },
            hostedService);

        var exception = await Assert.ThrowsAsync<UserFriendlyException>(() => service.EnableAsync());

        Assert.Equal("PLC 连接失败", exception.Message);
        Assert.False(appSettingsService.Current.IsWearPartMonitoringEnabled);
        Assert.Equal(0, appSettingsService.SaveCallCount);
    }

    [Fact]
    public async Task EnableAsync_WhenPlcConnectionSucceeds_ShouldEnableMonitoringAndRunOnce()
    {
        var appSettingsService = new StubAppSettingsService
        {
            Current = new AppSettings
            {
                ResourceNumber = "RES-01",
                IsWearPartMonitoringEnabled = false
            }
        };
        var monitorService = new TrackableWearPartMonitorService();
        var hostedService = CreateHostedService(appSettingsService, monitorService, PlcStartupConnectionResult.Connected());
        var service = new WearPartMonitoringControlService(
            appSettingsService,
            new StubPlcStartupConnectionService(),
            hostedService);

        await service.EnableAsync();

        Assert.True(appSettingsService.Current.IsWearPartMonitoringEnabled);
        Assert.Equal(1, appSettingsService.SaveCallCount);
        Assert.Equal(1, monitorService.CallCount);
    }

    private static WearPartMonitoringHostedService CreateHostedService(
        IAppSettingsService appSettingsService,
        IWearPartMonitorService monitorService,
        PlcStartupConnectionResult plcStartupConnectionResult)
    {
        return new WearPartMonitoringHostedService(
            new StubServiceScopeFactory(
                appSettingsService,
                monitorService,
                new StubPlcStartupConnectionService
                {
                    Result = plcStartupConnectionResult
                }),
            NullLogger<WearPartMonitoringHostedService>.Instance);
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

    private sealed class TrackableWearPartMonitorService : IWearPartMonitorService
    {
        public int CallCount { get; private set; }

        public Task<IReadOnlyList<WearPartMonitorResult>> MonitorByResourceNumberAsync(string resourceNumber, CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult<IReadOnlyList<WearPartMonitorResult>>([]);
        }

        public Task<IReadOnlyList<ExceedLimitRecord>> GetExceedLimitRecordsAsync(Guid clientAppConfigurationId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class StubPlcStartupConnectionService : IPlcStartupConnectionService
    {
        public PlcStartupConnectionResult Result { get; set; } = PlcStartupConnectionResult.Connected();

        public Task<PlcStartupConnectionResult> EnsureConnectedAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Result);
        }
    }

    private sealed class StubServiceScopeFactory : IServiceScopeFactory
    {
        private readonly IServiceProvider _serviceProvider;

        public StubServiceScopeFactory(
            IAppSettingsService appSettingsService,
            IWearPartMonitorService wearPartMonitorService,
            IPlcStartupConnectionService plcStartupConnectionService)
        {
            _serviceProvider = new StubServiceProvider(appSettingsService, wearPartMonitorService, plcStartupConnectionService);
        }

        public IServiceScope CreateScope() => new StubServiceScope(_serviceProvider);
    }

    private sealed class StubServiceScope : IServiceScope, IAsyncDisposable
    {
        public StubServiceScope(IServiceProvider serviceProvider)
        {
            ServiceProvider = serviceProvider;
        }

        public IServiceProvider ServiceProvider { get; }

        public void Dispose()
        {
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }

    private sealed class StubServiceProvider : IServiceProvider
    {
        private readonly Dictionary<Type, object> _services;

        public StubServiceProvider(
            IAppSettingsService appSettingsService,
            IWearPartMonitorService wearPartMonitorService,
            IPlcStartupConnectionService plcStartupConnectionService)
        {
            _services = new Dictionary<Type, object>
            {
                [typeof(IAppSettingsService)] = appSettingsService,
                [typeof(IWearPartMonitorService)] = wearPartMonitorService,
                [typeof(IPlcStartupConnectionService)] = plcStartupConnectionService
            };
        }

        public object? GetService(Type serviceType)
        {
            return _services.TryGetValue(serviceType, out var service)
                ? service
                : null;
        }
    }
}