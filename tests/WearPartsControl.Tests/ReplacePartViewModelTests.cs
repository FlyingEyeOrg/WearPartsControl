using System.Windows.Media;
using Microsoft.Extensions.DependencyInjection;
using WearPartsControl.ApplicationServices.AppSettings;
using WearPartsControl.ApplicationServices.ClientAppInfo;
using WearPartsControl.ApplicationServices.PlcService;
using WearPartsControl.ViewModels;
using Xunit;

namespace WearPartsControl.Tests;

public sealed class ReplacePartViewModelTests
{
    [Fact]
    public async Task EnsureConnectedAsync_WhenClientAppNotConfigured_ShouldSkipConnection()
    {
        var plcService = new StubPlcService();
        var plcConnectionStatusService = new PlcConnectionStatusService();
        var service = new PlcStartupConnectionService(
            new StubAppSettingsService
            {
                Current = new AppSettings
                {
                    IsSetClientAppInfo = false,
                    ResourceNumber = string.Empty
                }
            },
            new StubServiceScopeFactory(new StubClientAppInfoService()),
            plcService,
            plcConnectionStatusService);

        var result = await service.EnsureConnectedAsync();

        Assert.Equal(PlcStartupConnectionStatus.NotConfigured, result.Status);
        Assert.Equal("未配置 ClientApp，未连接 PLC。", result.Message);
        Assert.Equal(0, plcService.ConnectCount);
        Assert.Equal(PlcStartupConnectionStatus.NotConfigured, plcConnectionStatusService.Current.Status);
    }

    [Fact]
    public async Task EnsureConnectedAsync_WhenClientAppConfigured_ShouldConnectPlc()
    {
        var plcService = new StubPlcService();
        var plcConnectionStatusService = new PlcConnectionStatusService();
        var service = new PlcStartupConnectionService(
            new StubAppSettingsService
            {
                Current = new AppSettings
                {
                    IsSetClientAppInfo = true,
                    ResourceNumber = "RES-01"
                }
            },
            new StubServiceScopeFactory(
                new StubClientAppInfoService
                {
                    Model = new ClientAppInfoModel
                    {
                        ResourceNumber = "RES-01",
                        PlcProtocolType = "ModbusTcp",
                        PlcIpAddress = "192.168.0.10",
                        PlcPort = 502,
                        SiemensSlot = 1,
                        IsStringReverse = false
                    }
                }),
            plcService,
            plcConnectionStatusService);

        var result = await service.EnsureConnectedAsync();

        Assert.Equal(PlcStartupConnectionStatus.Connected, result.Status);
        Assert.Equal("已连接", result.Message);
        Assert.Equal(1, plcService.ConnectCount);
        Assert.Equal(PlcStartupConnectionStatus.Connected, plcConnectionStatusService.Current.Status);
        Assert.NotNull(plcService.LastOptions);
        Assert.Equal(PlcProtocolType.ModbusTcp, plcService.LastOptions!.PlcType);
        Assert.Equal("192.168.0.10", plcService.LastOptions.IpAddress);
        Assert.Equal(502, plcService.LastOptions.Port);
    }

    [Fact]
    public void StatusChanged_ShouldUpdateStatusWhenClientAppNotConfigured()
    {
        var plcConnectionStatusService = new PlcConnectionStatusService();
        var viewModel = new ReplacePartViewModel(plcConnectionStatusService);

        plcConnectionStatusService.Set(PlcStartupConnectionResult.NotConfigured());

        Assert.Equal("未配置 ClientApp，未连接 PLC。", viewModel.PlcConnectionStatusText);
        Assert.Same(Brushes.DimGray, viewModel.PlcConnectionStatusBackground);
    }

    [Fact]
    public void StatusChanged_ShouldUpdateStatusWhenConnectionSucceeded()
    {
        var plcConnectionStatusService = new PlcConnectionStatusService();
        var viewModel = new ReplacePartViewModel(plcConnectionStatusService);

        plcConnectionStatusService.Set(PlcStartupConnectionResult.Connected());

        Assert.Equal("已连接", viewModel.PlcConnectionStatusText);
        Assert.Same(Brushes.ForestGreen, viewModel.PlcConnectionStatusBackground);
    }

    private sealed class StubAppSettingsService : IAppSettingsService
    {
        public AppSettings Current { get; set; } = new();

        public event EventHandler<AppSettings>? SettingsSaved;

        public ValueTask<AppSettings> GetAsync(CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(new AppSettings
            {
                ResourceNumber = Current.ResourceNumber,
                LoginInputMaxIntervalMilliseconds = Current.LoginInputMaxIntervalMilliseconds,
                AutoLogoutCountdownSeconds = Current.AutoLogoutCountdownSeconds,
                IsSetClientAppInfo = Current.IsSetClientAppInfo
            });
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
        public ClientAppInfoModel Model { get; set; } = new();

        public Task<ClientAppInfoModel> GetAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Model);
        }

        public Task<ClientAppInfoModel> SaveAsync(ClientAppInfoSaveRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class StubPlcService : IPlcService
    {
        public bool IsConnected { get; private set; }

        public int ConnectCount { get; private set; }

        public PlcConnectionOptions? LastOptions { get; private set; }

        public void Connect(PlcConnectionOptions options)
        {
            LastOptions = options;
            ConnectCount++;
            IsConnected = true;
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
        private readonly IClientAppInfoService _clientAppInfoService;

        public StubServiceScopeFactory(IClientAppInfoService clientAppInfoService)
        {
            _clientAppInfoService = clientAppInfoService;
        }

        public IServiceScope CreateScope()
        {
            return new StubServiceScope(_clientAppInfoService);
        }
    }

    private sealed class StubServiceScope : IServiceScope, IAsyncDisposable
    {
        public StubServiceScope(IClientAppInfoService clientAppInfoService)
        {
            ServiceProvider = new StubScopedServiceProvider(clientAppInfoService);
        }

        public IServiceProvider ServiceProvider { get; }

        public void Dispose()
        {
        }

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }

    private sealed class StubScopedServiceProvider : IServiceProvider
    {
        private readonly IClientAppInfoService _clientAppInfoService;

        public StubScopedServiceProvider(IClientAppInfoService clientAppInfoService)
        {
            _clientAppInfoService = clientAppInfoService;
        }

        public object? GetService(Type serviceType)
        {
            return serviceType == typeof(IClientAppInfoService)
                ? _clientAppInfoService
                : null;
        }
    }
}