using System.Windows.Media;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using WearPartsControl.ApplicationServices.AppSettings;
using WearPartsControl.ApplicationServices.ClientAppInfo;
using WearPartsControl.ApplicationServices;
using WearPartsControl.ApplicationServices.Localization;
using WearPartsControl.ApplicationServices.PartServices;
using WearPartsControl.ApplicationServices.SaveInfoService;
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

        Assert.Null(viewModel.CodeMinLength);
        Assert.Null(viewModel.CodeMaxLength);
        Assert.Null(viewModel.CurrentValue);
        Assert.Null(viewModel.WarningValue);
        Assert.Null(viewModel.ShutdownValue);
    }

    [Fact]
    public async Task InitializeAsync_WhenDefinitionsExist_ShouldSelectFirstDefinitionAndLoadPreview()
    {
        var definition = new WearPartDefinition
        {
            Id = Guid.NewGuid(),
            PartName = "刀具A",
            InputMode = "Scanner",
            CodeMinLength = 8,
            CodeMaxLength = 32,
            CurrentValueDataType = "FLOAT",
            CurrentValueAddress = "DB1.0",
            WarningValueDataType = "FLOAT",
            WarningValueAddress = "DB1.2",
            ShutdownValueDataType = "FLOAT",
            ShutdownValueAddress = "DB1.4"
        };
        var replacementService = new StubWearPartReplacementService
        {
            Preview = new WearPartReplacementPreview
            {
                WearPartDefinitionId = definition.Id,
                ClientAppConfigurationId = Guid.NewGuid(),
                CurrentValue = "12.5",
                WarningValue = "20",
                ShutdownValue = "30",
                LastBarcode = null
            }
        };
        replacementService.History.Add(new WearPartReplacementRecord
        {
            WearPartDefinitionId = definition.Id,
            PartName = definition.PartName,
            NewBarcode = "BC-01"
        });

        var viewModel = new ReplacePartViewModel(
            new StubAppSettingsService { Current = new AppSettings { ResourceNumber = "RES-01" } },
            new StubClientAppInfoService { Model = new ClientAppInfoModel { ResourceNumber = "RES-01" } },
            new StubWearPartManagementService([definition]),
            replacementService,
            new StubToolChangeManagementService(),
            new StubToolChangeSelectionService(),
            new UiBusyService(TimeSpan.Zero),
            new PlcConnectionStatusService());

        await viewModel.InitializeAsync();

        Assert.Same(definition, viewModel.SelectedDefinition);
        Assert.Equal(12.5, viewModel.CurrentValue);
        Assert.Equal(20d, viewModel.WarningValue);
        Assert.Equal(30d, viewModel.ShutdownValue);
        Assert.Equal(LocalizedText.Get("ViewModels.ReplacePartVm.LastBarcodeEmpty"), viewModel.LastBarcode);
        Assert.Single(viewModel.ReplacementHistory);
    }

    [Fact]
    public async Task ReplaceAsync_WhenReplacementSucceeds_ShouldUpdateHistoryImmediately()
    {
        var clientAppConfigurationId = Guid.NewGuid();
        var definition = new WearPartDefinition
        {
            Id = Guid.NewGuid(),
            PartName = "刀具A",
            InputMode = "Scanner",
            CodeMinLength = 8,
            CodeMaxLength = 32,
            CurrentValueDataType = "FLOAT",
            CurrentValueAddress = "DB1.0",
            WarningValueDataType = "FLOAT",
            WarningValueAddress = "DB1.2",
            ShutdownValueDataType = "FLOAT",
            ShutdownValueAddress = "DB1.4"
        };
        var replacementService = new StubWearPartReplacementService
        {
            Preview = new WearPartReplacementPreview
            {
                WearPartDefinitionId = definition.Id,
                ClientAppConfigurationId = clientAppConfigurationId,
                CurrentValue = "12.5",
                WarningValue = "20",
                ShutdownValue = "30",
                LastBarcode = "BC-01"
            }
        };
        replacementService.History.Add(new WearPartReplacementRecord
        {
            Id = Guid.NewGuid(),
            ClientAppConfigurationId = clientAppConfigurationId,
            WearPartDefinitionId = definition.Id,
            PartName = definition.PartName,
            NewBarcode = "BC-01",
            ReplacementReason = WearPartReplacementReason.ProcessDamage,
            ReplacedAt = DateTime.UtcNow.AddMinutes(-1)
        });

        var viewModel = new ReplacePartViewModel(
            new StubAppSettingsService { Current = new AppSettings { ResourceNumber = "RES-01" } },
            new StubClientAppInfoService { Model = new ClientAppInfoModel { ResourceNumber = "RES-01" } },
            new StubWearPartManagementService([definition]),
            replacementService,
            new StubToolChangeManagementService(),
            new StubToolChangeSelectionService(),
            new UiBusyService(TimeSpan.Zero),
            new PlcConnectionStatusService());

        await viewModel.InitializeAsync();
        viewModel.SelectedReplacementReason = WearPartReplacementReason.ProcessDamage;
        viewModel.NewBarcode = "BC-02";
        viewModel.ReplacementMessage = "测试备注";

        await viewModel.ReplaceCommand.ExecuteAsync(null);

        Assert.Equal(string.Empty, viewModel.NewBarcode);
        Assert.Equal("BC-02", viewModel.LastBarcode);
        Assert.Equal(2, viewModel.ReplacementHistory.Count);
        Assert.Equal("BC-02", viewModel.ReplacementHistory[0].NewBarcode);
    }

    [Fact]
    public async Task InitializeAsync_WhenProcedureRequiresToolValidation_ShouldLoadSavedToolCode()
    {
        var definition = new WearPartDefinition
        {
            Id = Guid.NewGuid(),
            PartName = "刀具A",
            InputMode = "Scanner",
            CodeMinLength = 8,
            CodeMaxLength = 32,
            CurrentValueDataType = "FLOAT",
            CurrentValueAddress = "DB1.0",
            WarningValueDataType = "FLOAT",
            WarningValueAddress = "DB1.2",
            ShutdownValueDataType = "FLOAT",
            ShutdownValueAddress = "DB1.4"
        };
        var toolSelectionService = new StubToolChangeSelectionService();
        toolSelectionService.Selections[definition.Id] = "TL-01";
        var toolChangeManagementService = new StubToolChangeManagementService();
        toolChangeManagementService.Definitions.Add(new ToolChangeDefinition { Name = "刀型一", Code = "TL-01" });
        var viewModel = new ReplacePartViewModel(
            new StubAppSettingsService { Current = new AppSettings { ResourceNumber = "RES-01" } },
            new StubClientAppInfoService { Model = new ClientAppInfoModel { ResourceNumber = "RES-01", ProcedureCode = "模切分条" } },
            new StubWearPartManagementService([definition]),
            new StubWearPartReplacementService
            {
                Preview = new WearPartReplacementPreview
                {
                    WearPartDefinitionId = definition.Id,
                    ClientAppConfigurationId = Guid.NewGuid(),
                    CurrentValue = "10",
                    WarningValue = "20",
                    ShutdownValue = "30"
                }
            },
            toolChangeManagementService,
            toolSelectionService,
            new UiBusyService(TimeSpan.Zero),
            new PlcConnectionStatusService());

        await viewModel.InitializeAsync();

        Assert.True(viewModel.IsToolValidationEnabled);
        Assert.Equal("TL-01", viewModel.SelectedToolCode);
    }

    [Fact]
    public async Task InitializeAsync_WhenDefinitionHasAssociatedToolChange_ShouldPreferAssociatedToolCode()
    {
        var toolChangeId = Guid.NewGuid();
        var definition = new WearPartDefinition
        {
            Id = Guid.NewGuid(),
            PartName = "刀具A",
            InputMode = "Scanner",
            CodeMinLength = 8,
            CodeMaxLength = 32,
            CurrentValueDataType = "FLOAT",
            CurrentValueAddress = "DB1.0",
            WarningValueDataType = "FLOAT",
            WarningValueAddress = "DB1.2",
            ShutdownValueDataType = "FLOAT",
            ShutdownValueAddress = "DB1.4",
            ToolChangeId = toolChangeId
        };
        var toolSelectionService = new StubToolChangeSelectionService();
        toolSelectionService.Selections[definition.Id] = "TL-OLD";
        var toolChangeManagementService = new StubToolChangeManagementService();
        toolChangeManagementService.Definitions.Add(new ToolChangeDefinition { Id = toolChangeId, Name = "刀型一", Code = "TL-01" });
        toolChangeManagementService.Definitions.Add(new ToolChangeDefinition { Id = Guid.NewGuid(), Name = "刀型二", Code = "TL-OLD" });
        var viewModel = new ReplacePartViewModel(
            new StubAppSettingsService { Current = new AppSettings { ResourceNumber = "RES-01" } },
            new StubClientAppInfoService { Model = new ClientAppInfoModel { ResourceNumber = "RES-01", ProcedureCode = "模切分条" } },
            new StubWearPartManagementService([definition]),
            new StubWearPartReplacementService
            {
                Preview = new WearPartReplacementPreview
                {
                    WearPartDefinitionId = definition.Id,
                    ClientAppConfigurationId = Guid.NewGuid(),
                    CurrentValue = "10",
                    WarningValue = "20",
                    ShutdownValue = "30"
                }
            },
            toolChangeManagementService,
            toolSelectionService,
            new UiBusyService(TimeSpan.Zero),
            new PlcConnectionStatusService());

        await viewModel.InitializeAsync();

        Assert.Equal("TL-01", viewModel.SelectedToolCode);
    }

    private static ReplacePartViewModel CreateViewModel(IPlcConnectionStatusService plcConnectionStatusService)
    {
        return new ReplacePartViewModel(
            new StubAppSettingsService(),
            new StubClientAppInfoService(),
            new StubWearPartManagementService(),
            new StubWearPartReplacementService(),
            new StubToolChangeManagementService(),
            new StubToolChangeSelectionService(),
            new UiBusyService(TimeSpan.Zero),
            plcConnectionStatusService);
    }

    private sealed class StubToolChangeManagementService : IToolChangeManagementService
    {
        public List<ToolChangeDefinition> Definitions { get; } = [];

        public Task<IReadOnlyList<ToolChangeDefinition>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<ToolChangeDefinition>>(Definitions.ToArray());
        }

        public Task<ToolChangeDefinition> CreateAsync(ToolChangeDefinition definition, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<ToolChangeDefinition> UpdateAsync(ToolChangeDefinition definition, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
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
        private readonly IReadOnlyList<WearPartDefinition> _definitions;

        public StubWearPartManagementService(IReadOnlyList<WearPartDefinition>? definitions = null)
        {
            _definitions = definitions ?? [];
        }

        public Task<IReadOnlyList<WearPartDefinition>> GetDefinitionsByClientAppConfigurationAsync(Guid clientAppConfigurationId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_definitions);
        }

        public Task<IReadOnlyList<WearPartDefinition>> GetDefinitionsByResourceNumberAsync(string resourceNumber, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_definitions);
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
        public WearPartReplacementPreview Preview { get; set; } = new();

        public List<WearPartReplacementRecord> History { get; } = [];

        public Task<WearPartReplacementPreview> GetReplacementPreviewAsync(Guid wearPartDefinitionId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Preview);
        }

        public Task<WearPartReplacementRecord> ReplaceByScanAsync(WearPartReplacementRequest request, CancellationToken cancellationToken = default)
        {
            var record = new WearPartReplacementRecord
            {
                Id = Guid.NewGuid(),
                ClientAppConfigurationId = Preview.ClientAppConfigurationId,
                WearPartDefinitionId = request.WearPartDefinitionId,
                PartName = History.FirstOrDefault(x => x.WearPartDefinitionId == request.WearPartDefinitionId)?.PartName ?? "刀具A",
                OldBarcode = Preview.LastBarcode,
                NewBarcode = request.NewBarcode.Trim(),
                CurrentValue = Preview.CurrentValue,
                WarningValue = Preview.WarningValue,
                ShutdownValue = Preview.ShutdownValue,
                OperatorWorkNumber = "WORK-01",
                ReplacementReason = request.ReplacementReason.Trim(),
                ReplacementMessage = request.ReplacementMessage?.Trim() ?? string.Empty,
                ReplacedAt = DateTime.UtcNow
            };

            Preview = new WearPartReplacementPreview
            {
                WearPartDefinitionId = Preview.WearPartDefinitionId,
                ClientAppConfigurationId = Preview.ClientAppConfigurationId,
                ResourceNumber = Preview.ResourceNumber,
                PartName = record.PartName,
                LastBarcode = record.NewBarcode,
                CurrentValue = Preview.CurrentValue,
                WarningValue = Preview.WarningValue,
                ShutdownValue = Preview.ShutdownValue
            };

            History.Insert(0, record);
            return Task.FromResult(record);
        }

        public Task<IReadOnlyList<WearPartReplacementRecord>> GetReplacementHistoryAsync(Guid clientAppConfigurationId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<WearPartReplacementRecord>>(History);
        }
    }

    private sealed class StubToolChangeSelectionService : IToolChangeSelectionService
    {
        public Dictionary<Guid, string> Selections { get; } = new();

        public ValueTask<ToolChangeSelectionState> GetStateAsync(Guid wearPartDefinitionId, CancellationToken cancellationToken = default)
        {
            Selections.TryGetValue(wearPartDefinitionId, out var selected);
            return ValueTask.FromResult(new ToolChangeSelectionState
            {
                SelectedToolCode = selected ?? string.Empty,
                RecentToolCodes = Selections.Values.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
            });
        }

        public ValueTask SaveSelectionAsync(Guid wearPartDefinitionId, string toolCode, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(toolCode))
            {
                Selections.Remove(wearPartDefinitionId);
            }
            else
            {
                Selections[wearPartDefinitionId] = toolCode.Trim();
            }

            return ValueTask.CompletedTask;
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