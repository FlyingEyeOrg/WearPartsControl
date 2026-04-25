using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WearPartsControl.ApplicationServices.AppSettings;
using WearPartsControl.ApplicationServices.ClientAppInfo;
using WearPartsControl.ApplicationServices.Localization;
using WearPartsControl.ApplicationServices.PlcService;
using Xunit;

namespace WearPartsControl.Tests;

public sealed class PlcConfigurationMonitorServiceTests
{
    [Fact]
    public async Task SettingsSaved_WhenPlcConfigurationChanges_ShouldReconnectImmediately()
    {
        var appSettingsService = new StubAppSettingsService();
        var clientAppInfoService = new StubClientAppInfoService();
        var plcService = new StubPlcService();
        var plcOperationPipeline = new PlcOperationPipeline(plcService, new TestLogger<PlcOperationPipeline>());
        var connectionStatusService = new PlcConnectionStatusService();
        var configurationResolver = new PlcClientConfigurationResolver(appSettingsService, new StubServiceScopeFactory(clientAppInfoService));
        using var monitorService = new PlcConfigurationMonitorService(
            appSettingsService,
            configurationResolver,
            plcOperationPipeline,
            connectionStatusService,
            new TestLogger<PlcConfigurationMonitorService>());

        clientAppInfoService.Current = CreateClientAppInfoModel(ipAddress: "192.168.0.10");

        await appSettingsService.SaveAsync(new AppSettings
        {
            IsSetClientAppInfo = true,
            ResourceNumber = clientAppInfoService.Current.ResourceNumber
        });

        await WaitForAsync(() => plcService.ConnectCalls.Count == 1);

        Assert.Single(plcService.ConnectCalls);
        Assert.Equal("192.168.0.10", plcService.ConnectCalls[0].IpAddress);
        Assert.Equal(PlcStartupConnectionStatus.Connected, connectionStatusService.Current.Status);
    }

    [Fact]
    public async Task SettingsSaved_WhenSameConfigurationIsSavedAgain_ShouldSkipDuplicateReconnect()
    {
        var appSettingsService = new StubAppSettingsService();
        var clientAppInfoService = new StubClientAppInfoService
        {
            Current = CreateClientAppInfoModel(ipAddress: "192.168.0.10")
        };
        var plcService = new StubPlcService();
        var plcOperationPipeline = new PlcOperationPipeline(plcService, new TestLogger<PlcOperationPipeline>());
        var connectionStatusService = new PlcConnectionStatusService();
        var configurationResolver = new PlcClientConfigurationResolver(appSettingsService, new StubServiceScopeFactory(clientAppInfoService));
        using var monitorService = new PlcConfigurationMonitorService(
            appSettingsService,
            configurationResolver,
            plcOperationPipeline,
            connectionStatusService,
            new TestLogger<PlcConfigurationMonitorService>());

        var settings = new AppSettings
        {
            IsSetClientAppInfo = true,
            ResourceNumber = clientAppInfoService.Current.ResourceNumber
        };

        await appSettingsService.SaveAsync(settings);
        await WaitForAsync(() => plcService.ConnectCalls.Count == 1);

        await appSettingsService.SaveAsync(settings);
        await Task.Delay(150);

        Assert.Single(plcService.ConnectCalls);
    }

    [Fact]
    public async Task SettingsSaved_WhenReconnectFailsButCurrentConnectionExists_ShouldKeepConnectedStatus()
    {
        using var cultureScope = new TestCultureScope("zh-CN");
        var appSettingsService = new StubAppSettingsService();
        var clientAppInfoService = new StubClientAppInfoService
        {
            Current = CreateClientAppInfoModel(ipAddress: "192.168.0.10")
        };
        var plcService = new StubPlcService();
        var plcOperationPipeline = new PlcOperationPipeline(plcService, new TestLogger<PlcOperationPipeline>());
        var connectionStatusService = new PlcConnectionStatusService();
        var configurationResolver = new PlcClientConfigurationResolver(appSettingsService, new StubServiceScopeFactory(clientAppInfoService));
        using var monitorService = new PlcConfigurationMonitorService(
            appSettingsService,
            configurationResolver,
            plcOperationPipeline,
            connectionStatusService,
            new TestLogger<PlcConfigurationMonitorService>());

        await appSettingsService.SaveAsync(new AppSettings
        {
            IsSetClientAppInfo = true,
            ResourceNumber = clientAppInfoService.Current.ResourceNumber
        });
        await WaitForAsync(() => plcService.ConnectCalls.Count == 1);

        plcService.ThrowOnConnect = new InvalidOperationException("network down");
        clientAppInfoService.Current = CreateClientAppInfoModel(ipAddress: "192.168.0.11");
        var expectedMessage = LocalizedText.Format("Services.PlcStartupConnection.ReconfiguredFailedKeepCurrent", "network down");

        await appSettingsService.SaveAsync(new AppSettings
        {
            IsSetClientAppInfo = true,
            ResourceNumber = clientAppInfoService.Current.ResourceNumber
        });

        await WaitForAsync(() => string.Equals(connectionStatusService.Current.Message, expectedMessage, StringComparison.Ordinal));

        Assert.Equal(2, plcService.ConnectCalls.Count);
        Assert.Equal(PlcStartupConnectionStatus.Connected, connectionStatusService.Current.Status);
        Assert.Equal(expectedMessage, connectionStatusService.Current.Message);
        Assert.True(plcService.IsConnected);
    }

    [Fact]
    public async Task SettingsSaved_WhenLoadingClientConfigurationThrows_ShouldSetFailedStatus()
    {
        var appSettingsService = new StubAppSettingsService();
        var clientAppInfoService = new StubClientAppInfoService
        {
            ThrowOnGet = new InvalidOperationException("config missing")
        };
        var plcService = new StubPlcService();
        var plcOperationPipeline = new PlcOperationPipeline(plcService, new TestLogger<PlcOperationPipeline>());
        var connectionStatusService = new PlcConnectionStatusService();
        var configurationResolver = new PlcClientConfigurationResolver(appSettingsService, new StubServiceScopeFactory(clientAppInfoService));
        using var monitorService = new PlcConfigurationMonitorService(
            appSettingsService,
            configurationResolver,
            plcOperationPipeline,
            connectionStatusService,
            new TestLogger<PlcConfigurationMonitorService>());

        await appSettingsService.SaveAsync(new AppSettings
        {
            IsSetClientAppInfo = true,
            ResourceNumber = "RES-PLC-01"
        });

        await WaitForAsync(() => connectionStatusService.Current.Status == PlcStartupConnectionStatus.Failed);

        Assert.Empty(plcService.ConnectCalls);
        Assert.Equal(
            LocalizedText.Format("Services.PlcStartupConnection.ConnectFailed", "config missing"),
            connectionStatusService.Current.Message);
    }

    private static ClientAppInfoModel CreateClientAppInfoModel(string ipAddress)
    {
        return new ClientAppInfoModel
        {
            ResourceNumber = "RES-PLC-01",
            SiteCode = "S01",
            FactoryCode = "F01",
            AreaCode = "A01",
            ProcedureCode = "P01",
            EquipmentCode = "EQ01",
            PlcProtocolType = "SiemensS1500",
            PlcIpAddress = ipAddress,
            PlcPort = 102,
            ShutdownPointAddress = "M0.0",
            SiemensRack = 0,
            SiemensSlot = 0,
            IsStringReverse = false
        };
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(50);
        }

        Assert.True(condition(), "Condition was not met in time.");
    }

    private sealed class StubAppSettingsService : IAppSettingsService
    {
        public AppSettings Current { get; private set; } = new();

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

    private sealed class StubClientAppInfoService : IClientAppInfoService
    {
        public ClientAppInfoModel Current { get; set; } = CreateClientAppInfoModel("192.168.0.10");

        public Exception? ThrowOnGet { get; set; }

        public Task<ClientAppInfoModel> GetAsync(CancellationToken cancellationToken = default)
        {
            if (ThrowOnGet is not null)
            {
                throw ThrowOnGet;
            }

            return Task.FromResult(Current);
        }

        public Task<ClientAppInfoModel> SaveAsync(ClientAppInfoSaveRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class StubPlcService : IPlcService
    {
        public List<PlcConnectionOptions> ConnectCalls { get; } = new();

        public Exception? ThrowOnConnect { get; set; }

        public bool IsConnected { get; private set; }

        public Task ConnectAsync(PlcConnectionOptions options, CancellationToken cancellationToken = default)
        {
            ConnectCalls.Add(options);
            if (ThrowOnConnect is not null)
            {
                throw ThrowOnConnect;
            }

            IsConnected = true;
            return Task.CompletedTask;
        }

        public void Disconnect()
        {
            IsConnected = false;
        }

        public TValue Read<TValue>(string address, int retryCount = 1)
        {
            throw new NotSupportedException();
        }

        public void Write<TValue>(string address, TValue value, int retryCount = 1)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class StubServiceScopeFactory : IServiceScopeFactory
    {
        private readonly IServiceProvider _serviceProvider;

        public StubServiceScopeFactory(IClientAppInfoService clientAppInfoService)
        {
            _serviceProvider = new StubServiceProvider(clientAppInfoService);
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

        public StubServiceProvider(IClientAppInfoService clientAppInfoService)
        {
            _services = new Dictionary<Type, object>
            {
                [typeof(IClientAppInfoService)] = clientAppInfoService
            };
        }

        public object? GetService(Type serviceType)
        {
            return _services.TryGetValue(serviceType, out var service)
                ? service
                : null;
        }
    }

    private sealed class TestLogger<T> : ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
        }
    }
}