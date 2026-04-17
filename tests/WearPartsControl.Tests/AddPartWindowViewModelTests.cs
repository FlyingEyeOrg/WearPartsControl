using WearPartsControl.ApplicationServices;
using WearPartsControl.ApplicationServices.PartServices;
using WearPartsControl.ViewModels;
using Xunit;

namespace WearPartsControl.Tests;

public sealed class AddPartWindowViewModelTests
{
    [Fact]
    public void InitializeForCreate_ShouldUseLegacyAlignedDefaults()
    {
        var viewModel = new AddPartWindowViewModel(new StubWearPartManagementService(), new UiBusyService());

        viewModel.InitializeForCreate(Guid.NewGuid(), "RES-001");

        Assert.Equal("Manual", viewModel.InputMode);
        Assert.Equal("FLOAT", viewModel.CurrentValueDataType);
        Assert.Equal("FLOAT", viewModel.WarningValueDataType);
        Assert.Equal("FLOAT", viewModel.ShutdownValueDataType);
        Assert.Equal("Meter", viewModel.LifetimeType);
        Assert.Equal("0", viewModel.CodeMinLength);
        Assert.Equal("0", viewModel.CodeMaxLength);
        Assert.False(viewModel.IsShutdown);
        Assert.Equal(string.Empty, viewModel.PlcZeroClearAddress);
        Assert.Equal(string.Empty, viewModel.BarcodeWriteAddress);
    }

    [Fact]
    public void InitializeForEdit_ShouldPreserveExistingDefinitionValues()
    {
        var viewModel = new AddPartWindowViewModel(new StubWearPartManagementService(), new UiBusyService());
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
            LifetimeType = "Count",
            PlcZeroClearAddress = "DB1.3",
            BarcodeWriteAddress = "DB1.4"
        };

        viewModel.InitializeForEdit(definition);

        Assert.Equal("Barcode", viewModel.InputMode);
        Assert.Equal("INT32", viewModel.CurrentValueDataType);
        Assert.Equal("BOOL", viewModel.WarningValueDataType);
        Assert.Equal("STRING", viewModel.ShutdownValueDataType);
        Assert.Equal("Count", viewModel.LifetimeType);
        Assert.Equal("6", viewModel.CodeMinLength);
        Assert.Equal("18", viewModel.CodeMaxLength);
        Assert.True(viewModel.IsShutdown);
        Assert.Equal("DB1.3", viewModel.PlcZeroClearAddress);
        Assert.Equal("DB1.4", viewModel.BarcodeWriteAddress);
    }

    private sealed class StubWearPartManagementService : IWearPartManagementService
    {
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
}