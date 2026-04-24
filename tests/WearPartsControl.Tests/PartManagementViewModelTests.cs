using WearPartsControl.ApplicationServices;
using WearPartsControl.ApplicationServices.ClientAppInfo;
using WearPartsControl.ApplicationServices.LegacyImport;
using WearPartsControl.ApplicationServices.Localization;
using WearPartsControl.ApplicationServices.PartServices;
using WearPartsControl.ViewModels;
using Xunit;

namespace WearPartsControl.Tests;

[Collection(LocalizationSensitiveTestCollection.Name)]
public sealed class PartManagementViewModelTests
{
    [Fact]
    public async Task ImportLegacyDefinitionsAsync_ShouldCallLegacyImportServiceAndUpdateStatus()
    {
        var legacyImportService = new StubLegacyDatabaseImportService();
        var uiDispatcher = new StubUiDispatcher();
        var viewModel = new PartManagementViewModel(
            new StubClientAppInfoService(),
            legacyImportService,
            new StubWearPartManagementService(),
            uiDispatcher,
            new UiBusyService(TimeSpan.Zero));

        await viewModel.InitializeAsync();

        var result = await viewModel.ImportLegacyDefinitionsAsync("E:\\legacy\\Data.db");

        Assert.Equal("E:\\legacy\\Data.db", legacyImportService.LastPath);
        Assert.Equal(2, result.ImportedWearPartDefinitions);
        Assert.Contains("新增 2 条", viewModel.StatusMessage);
        Assert.True(uiDispatcher.RenderCount >= 2);
    }

    [Fact]
    public void ImportLegacyDefinitionsCommand_ShouldRaiseRequestedEvent()
    {
        var viewModel = new PartManagementViewModel(
            new StubClientAppInfoService(),
            new StubLegacyDatabaseImportService(),
            new StubWearPartManagementService(),
            new StubUiDispatcher(),
            new UiBusyService(TimeSpan.Zero));
        var raised = false;
        viewModel.ImportLegacyDefinitionsRequested += (_, _) => raised = true;

        viewModel.ImportLegacyDefinitionsCommand.Execute(null);

        Assert.True(raised);
    }

    [Fact]
    public void LocalizationRefresh_ShouldUpdateDefaultStatusMessage()
    {
        using var cultureScope = new TestCultureScope("zh-CN");
        var viewModel = new PartManagementViewModel(
            new StubClientAppInfoService(),
            new StubLegacyDatabaseImportService(),
            new StubWearPartManagementService(),
            new StubUiDispatcher(),
            new UiBusyService(TimeSpan.Zero));

        using var _ = new TestCultureScope("en-US");
        LocalizationBindingSource.Instance.Refresh();

        Assert.Equal(LocalizedText.Get("ViewModels.PartManagementVm.PromptLoadCurrent"), viewModel.StatusMessage);
    }

    private sealed class StubClientAppInfoService : IClientAppInfoService
    {
        private readonly Guid _id = Guid.NewGuid();

        public Task<ClientAppInfoModel> GetAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ClientAppInfoModel
            {
                Id = _id,
                ResourceNumber = "RES-01"
            });
        }

        public Task<ClientAppInfoModel> SaveAsync(ClientAppInfoSaveRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class StubLegacyDatabaseImportService : ILegacyDatabaseImportService
    {
        public string? LastPath { get; private set; }

        public Task<LegacyDatabaseImportResult> ImportAsync(string legacyDatabasePath, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<LegacyDatabaseImportResult> ImportWearPartDefinitionsAsync(string legacyDatabasePath, Guid clientAppConfigurationId, string resourceNumber, CancellationToken cancellationToken = default)
        {
            LastPath = legacyDatabasePath;
            return Task.FromResult(new LegacyDatabaseImportResult
            {
                LegacyDatabasePath = legacyDatabasePath,
                ImportedWearPartDefinitions = 2,
                UpdatedWearPartDefinitions = 1,
                SkippedRows = 0
            });
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
            throw new NotSupportedException();
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

    private sealed class StubUiDispatcher : IUiDispatcher
    {
        public int RenderCount { get; private set; }

        public void Run(Action action) => action();

        public Task RunAsync(Action action, System.Windows.Threading.DispatcherPriority priority = System.Windows.Threading.DispatcherPriority.Normal)
        {
            action();
            return Task.CompletedTask;
        }

        public Task RenderAsync()
        {
            RenderCount++;
            return Task.CompletedTask;
        }
    }
}