using WearPartsControl.ApplicationServices;
using WearPartsControl.ApplicationServices.Localization;
using WearPartsControl.ApplicationServices.PartServices;
using WearPartsControl.ViewModels;
using Xunit;

namespace WearPartsControl.Tests;

[Collection(LocalizationSensitiveTestCollection.Name)]
public sealed class AddPartWindowViewModelTests
{
    [Fact]
    public async Task InitializeForCreate_ShouldUseLegacyAlignedDefaults()
    {
        using var cultureScope = new TestCultureScope("zh-CN");
        var viewModel = new AddPartWindowViewModel(new StubWearPartManagementService(), new StubWearPartTypeService(), new UiBusyService());

        await viewModel.InitializeForCreateAsync(Guid.NewGuid(), "RES-001");

        Assert.Equal("Manual", viewModel.InputMode);
        Assert.Equal("FLOAT", viewModel.CurrentValueDataType);
        Assert.Equal("FLOAT", viewModel.WarningValueDataType);
        Assert.Equal("FLOAT", viewModel.ShutdownValueDataType);
        Assert.Equal("Count", viewModel.LifetimeType);
        Assert.Equal(
            LocalizedText.Get("ViewModels.WearPartEditorVm.LifetimeTypeCount"),
            Assert.Single(viewModel.LifetimeTypes, option => option.Code == "Count").DisplayName);
        Assert.Equal("0", viewModel.CodeMinLength);
        Assert.Equal("0", viewModel.CodeMaxLength);
        Assert.False(viewModel.IsShutdown);
        Assert.Equal(string.Empty, viewModel.PlcZeroClearAddress);
        Assert.Equal(string.Empty, viewModel.BarcodeWriteAddress);
    }

    [Fact]
    public async Task InitializeForEdit_ShouldPreserveExistingDefinitionValues()
    {
        var typeService = new StubWearPartTypeService();
        var viewModel = new AddPartWindowViewModel(new StubWearPartManagementService(), typeService, new UiBusyService());
        var selectedType = typeService.Types[1];
        var definition = new WearPartDefinition
        {
            Id = Guid.NewGuid(),
            ClientAppConfigurationId = Guid.NewGuid(),
            ResourceNumber = "RES-002",
            PartName = "刀具A",
            InputMode = "Barcode",
            CurrentValueAddress = "DB1.0",
            CurrentValueDataType = "INT32",
            WarningValueAddress = "DB1.1",
            WarningValueDataType = "BOOL",
            ShutdownValueAddress = "DB1.2",
            ShutdownValueDataType = "STRING",
            IsShutdown = true,
            CodeMinLength = 6,
            CodeMaxLength = 18,
            LifetimeType = "计次",
            WearPartTypeId = selectedType.Id,
            ToolChangeId = Guid.NewGuid(),
            PlcZeroClearAddress = "DB1.3",
            BarcodeWriteAddress = "DB1.4"
        };

        await viewModel.InitializeForEditAsync(definition);

        Assert.Equal("Barcode", viewModel.InputMode);
        Assert.Equal("INT32", viewModel.CurrentValueDataType);
        Assert.Equal("BOOL", viewModel.WarningValueDataType);
        Assert.Equal("STRING", viewModel.ShutdownValueDataType);
        Assert.Equal("Count", viewModel.LifetimeType);
        Assert.Equal("6", viewModel.CodeMinLength);
        Assert.Equal("18", viewModel.CodeMaxLength);
        Assert.True(viewModel.IsShutdown);
        Assert.Equal(selectedType.Id, viewModel.SelectedWearPartTypeId);
        Assert.Equal("DB1.3", viewModel.PlcZeroClearAddress);
        Assert.Equal("DB1.4", viewModel.BarcodeWriteAddress);
    }

    [Fact]
    public async Task SaveCommand_WhenZeroClearAddressEmpty_ShouldStillAllowSave()
    {
        var service = new StubWearPartManagementService();
        var viewModel = new AddPartWindowViewModel(service, new StubWearPartTypeService(), new UiBusyService(TimeSpan.Zero));

        await viewModel.InitializeForCreateAsync(Guid.NewGuid(), "RES-003");
        viewModel.PartName = "刀具B";
        viewModel.CurrentValueAddress = "DB1.0";
        viewModel.WarningValueAddress = "DB1.1";
        viewModel.ShutdownValueAddress = "DB1.2";
        viewModel.CodeMinLength = "1";
        viewModel.CodeMaxLength = "12";
        viewModel.PlcZeroClearAddress = string.Empty;

        Assert.True(viewModel.SaveCommand.CanExecute(null));

        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.NotNull(service.LastCreatedDefinition);
        Assert.Equal(string.Empty, service.LastCreatedDefinition!.PlcZeroClearAddress);
    }

    [Fact]
    public async Task SaveCommand_ShouldNotPersistToolChangeId()
    {
        var service = new StubWearPartManagementService();
        var viewModel = new AddPartWindowViewModel(service, new StubWearPartTypeService(), new UiBusyService(TimeSpan.Zero));

        await viewModel.InitializeForCreateAsync(Guid.NewGuid(), "RES-004");
        viewModel.PartName = "刀具C";
        viewModel.CurrentValueAddress = "DB1.0";
        viewModel.WarningValueAddress = "DB1.1";
        viewModel.ShutdownValueAddress = "DB1.2";
        viewModel.CodeMinLength = "1";
        viewModel.CodeMaxLength = "12";

        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.Null(service.LastCreatedDefinition?.ToolChangeId);
    }

    [Fact]
    public async Task LocalizationRefresh_ShouldUpdateEditorStatusMessage()
    {
        using var cultureScope = new TestCultureScope("zh-CN");
        var viewModel = new AddPartWindowViewModel(new StubWearPartManagementService(), new StubWearPartTypeService(), new UiBusyService());

        await viewModel.InitializeForCreateAsync(Guid.NewGuid(), "RES-001");

        using var _ = new TestCultureScope("en-US");
        LocalizationBindingSource.Instance.Refresh();

        Assert.Equal(LocalizedText.Format("ViewModels.WearPartEditorVm.CurrentResourceNumber", "RES-001"), viewModel.StatusMessage);
    }

    [Fact]
    public async Task LocalizationRefresh_ShouldUpdateLifetimeTypeDisplayNameForCurrentSelection()
    {
        using var cultureScope = new TestCultureScope("zh-CN");
        var viewModel = new AddPartWindowViewModel(new StubWearPartManagementService(), new StubWearPartTypeService(), new UiBusyService());

        await viewModel.InitializeForCreateAsync(Guid.NewGuid(), "RES-001");

        Assert.Equal("Count", viewModel.LifetimeType);
        Assert.Equal("计次", Assert.Single(viewModel.LifetimeTypes, option => option.Code == "Count").DisplayName);

        using var _ = new TestCultureScope("en-US");
        LocalizationBindingSource.Instance.Refresh();

        Assert.Equal("Count", viewModel.LifetimeType);
        Assert.Equal("Count", Assert.Single(viewModel.LifetimeTypes, option => option.Code == "Count").DisplayName);
    }

    private sealed class StubWearPartManagementService : IWearPartManagementService
    {
        public WearPartDefinition? LastCreatedDefinition { get; private set; }

        public Task<IReadOnlyList<WearPartDefinition>> GetDefinitionsByClientAppConfigurationAsync(Guid clientAppConfigurationId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyList<WearPartDefinition>> GetDefinitionsByResourceNumberAsync(string resourceNumber, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<WearPartDefinition?> GetDefinitionAsync(Guid id, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<WearPartDefinition> CreateDefinitionAsync(WearPartDefinition definition, CancellationToken cancellationToken = default)
        {
            LastCreatedDefinition = definition;
            return Task.FromResult(definition);
        }

        public Task<WearPartDefinition> UpdateDefinitionAsync(WearPartDefinition definition, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(definition);
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

    private sealed class StubWearPartTypeService : IWearPartTypeService
    {
        public List<WearPartTypeDefinition> Types { get; } =
        [
            new() { Id = Guid.NewGuid(), Code = WearPartTypeCodes.Uncategorized, Name = "未分类" },
            new() { Id = Guid.NewGuid(), Code = WearPartTypeCodes.Cutter, Name = "切刀" }
        ];

        public Task<IReadOnlyList<WearPartTypeDefinition>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<WearPartTypeDefinition>>(Types);
        }
    }
}