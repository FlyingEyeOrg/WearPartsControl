using System.Windows.Media;
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
        var service = new PlcStartupConnectionService(
            new StubAppSettingsService
            {
                Current = new AppSettings
                {
                    IsSetClientAppInfo = false,
                    ResourceNumber = string.Empty
                }
            },
            new StubClientAppInfoService(),
            plcService);

        var result = await service.EnsureConnectedAsync();

        Assert.Equal(PlcStartupConnectionStatus.NotConfigured, result.Status);
        Assert.Equal("未配置 ClientApp，未连接 PLC。", result.Message);
        Assert.Equal(0, plcService.ConnectCount);
    }

    [Fact]
    public async Task EnsureConnectedAsync_WhenClientAppConfigured_ShouldConnectPlc()
    {
        var plcService = new StubPlcService();
        var service = new PlcStartupConnectionService(
            new StubAppSettingsService
            {
                Current = new AppSettings
                {
                    IsSetClientAppInfo = true,
                    ResourceNumber = "RES-01"
                }
            },
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
            },
            plcService);

        var result = await service.EnsureConnectedAsync();

        Assert.Equal(PlcStartupConnectionStatus.Connected, result.Status);
        Assert.Equal("已连接", result.Message);
        Assert.Equal(1, plcService.ConnectCount);
        Assert.NotNull(plcService.LastOptions);
        Assert.Equal(PlcProtocolType.ModbusTcp, plcService.LastOptions!.PlcType);
        Assert.Equal("192.168.0.10", plcService.LastOptions.IpAddress);
        Assert.Equal(502, plcService.LastOptions.Port);
    }

    [Fact]
    public async Task InitializeAsync_ShouldUpdateStatusWhenClientAppNotConfigured()
    {
        var viewModel = new ReplacePartViewModel(new StubPlcStartupConnectionService(
            PlcStartupConnectionResult.NotConfigured()));

        await viewModel.InitializeAsync();

        Assert.Equal("未配置 ClientApp，未连接 PLC。", viewModel.PlcConnectionStatusText);
        Assert.Same(Brushes.DimGray, viewModel.PlcConnectionStatusBackground);
    }

    [Fact]
    public async Task InitializeAsync_ShouldUpdateStatusWhenConnectionSucceeded()
    {
        var viewModel = new ReplacePartViewModel(new StubPlcStartupConnectionService(
            PlcStartupConnectionResult.Connected()));

        await viewModel.InitializeAsync();

        Assert.Equal("已连接", viewModel.PlcConnectionStatusText);
        Assert.Same(Brushes.ForestGreen, viewModel.PlcConnectionStatusBackground);
    }

    private sealed class StubPlcStartupConnectionService : IPlcStartupConnectionService
    {
        private readonly PlcStartupConnectionResult _result;

        public StubPlcStartupConnectionService(PlcStartupConnectionResult result)
        {
            _result = result;
        }

        public Task<PlcStartupConnectionResult> EnsureConnectedAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_result);
        }
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
}