using System.Windows.Media;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using WearPartsControl.ApplicationServices.AppSettings;
using WearPartsControl.ApplicationServices.ClientAppInfo;
using WearPartsControl.ApplicationServices;
using WearPartsControl.ApplicationServices.Localization;
using WearPartsControl.ApplicationServices.PartServices;
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
        var plcOperationPipeline = new PlcOperationPipeline(plcService, NullLogger<PlcOperationPipeline>.Instance);
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
            plcOperationPipeline,
            plcConnectionStatusService,
            NullLogger<PlcStartupConnectionService>.Instance);

        var result = await service.EnsureConnectedAsync();

        Assert.Equal(PlcStartupConnectionStatus.NotConfigured, result.Status);
        Assert.Equal(LocalizedText.Get("Services.PlcStartupConnection.NotConfigured"), result.Message);
        Assert.Equal(0, plcService.ConnectCount);
        Assert.Equal(PlcStartupConnectionStatus.NotConfigured, plcConnectionStatusService.Current.Status);
    }

    [Fact]
    public async Task EnsureConnectedAsync_WhenClientAppConfigured_ShouldConnectPlc()
    {
        var plcService = new StubPlcService();
        var plcOperationPipeline = new PlcOperationPipeline(plcService, NullLogger<PlcOperationPipeline>.Instance);
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
                        SiemensRack = 0,
                        SiemensSlot = 0,
                        IsStringReverse = false
                    }
                }),
            plcOperationPipeline,
            plcConnectionStatusService,
            NullLogger<PlcStartupConnectionService>.Instance);

        var result = await service.EnsureConnectedAsync();

        Assert.Equal(PlcStartupConnectionStatus.Connected, result.Status);
        Assert.Equal(LocalizedText.Get("Services.PlcStartupConnection.Connected"), result.Message);
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
        var viewModel = CreateViewModel(plcConnectionStatusService);

        plcConnectionStatusService.Set(PlcStartupConnectionResult.NotConfigured());

        Assert.Equal(LocalizedText.Get("Services.PlcStartupConnection.NotConfigured"), viewModel.PlcConnectionStatusText);
        Assert.Same(Brushes.DimGray, viewModel.PlcConnectionStatusBackground);
    }

    [Fact]
    public void StatusChanged_ShouldUpdateStatusWhenConnectionSucceeded()
    {
        var plcConnectionStatusService = new PlcConnectionStatusService();
        var viewModel = CreateViewModel(plcConnectionStatusService);

        plcConnectionStatusService.Set(PlcStartupConnectionResult.Connected());

        Assert.Equal(plcConnectionStatusService.Current.Message, viewModel.PlcConnectionStatusText);
        Assert.Same(Brushes.ForestGreen, viewModel.PlcConnectionStatusBackground);
    }

    [Fact]
    public void Created_WhenNoDefinitionSelected_ShouldKeepNumericDisplayFieldsEmpty()
    {
        var viewModel = CreateViewModel(new PlcConnectionStatusService());

        Assert.Equal(string.Empty, viewModel.CodeMinLengthText);
        Assert.Equal(string.Empty, viewModel.CodeMaxLengthText);
        Assert.Equal(string.Empty, viewModel.CurrentValue);
        Assert.Equal(string.Empty, viewModel.WarningValue);
        Assert.Equal(string.Empty, viewModel.ShutdownValue);
    }

    private static ReplacePartViewModel CreateViewModel(IPlcConnectionStatusService plcConnectionStatusService)
    {
        return new ReplacePartViewModel(
            new StubAppSettingsService(),
            new StubWearPartManagementService(),
            new StubWearPartReplacementService(),
            new UiBusyService(TimeSpan.Zero),
            plcConnectionStatusService);
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

        public Task ConnectAsync(PlcConnectionOptions options, CancellationToken cancellationToken = default)
        {
            LastOptions = options;
            ConnectCount++;
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

    private sealed class StubWearPartManagementService : IWearPartManagementService
    {
        public Task<IReadOnlyList<WearPartDefinition>> GetDefinitionsByClientAppConfigurationAsync(Guid clientAppConfigurationId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<WearPartDefinition>>([]);
        }

        public Task<IReadOnlyList<WearPartDefinition>> GetDefinitionsByResourceNumberAsync(string resourceNumber, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<WearPartDefinition>>([]);
        }

        public Task<WearPartDefinition?> GetDefinitionAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<WearPartDefinition?>(null);
        }

        public Task<WearPartDefinition> CreateDefinitionAsync(WearPartDefinition definition, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<WearPartDefinition> UpdateDefinitionAsync(WearPartDefinition definition, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task DeleteDefinitionAsync(Guid id, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<int> CopyDefinitionsAsync(string sourceResourceNumber, string targetResourceNumber, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class StubWearPartReplacementService : IWearPartReplacementService
    {
        public Task<WearPartReplacementPreview> GetReplacementPreviewAsync(Guid wearPartDefinitionId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<WearPartReplacementRecord> ReplaceByScanAsync(WearPartReplacementRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyList<WearPartReplacementRecord>> GetReplacementHistoryAsync(Guid clientAppConfigurationId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<WearPartReplacementRecord>>([]);
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