using WearPartsControl.ApplicationServices;
using WearPartsControl.ApplicationServices.ClientAppInfo;
using WearPartsControl.ApplicationServices.LoginService;
using WearPartsControl.ApplicationServices.Dialogs;
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
        using var cultureScope = new TestCultureScope("zh-CN");
        var legacyImportService = new StubLegacyDatabaseImportService();
        var uiDispatcher = new StubUiDispatcher();
        var viewModel = new PartManagementViewModel(
            new StubClientAppInfoService(),
            legacyImportService,
            new StubWearPartManagementService(),
            CreateCurrentUserAccessor(4),
            uiDispatcher,
            new UiBusyService(TimeSpan.Zero),
            new StubAppDialogService());

        await viewModel.InitializeAsync();

        var result = await viewModel.ImportLegacyDefinitionsAsync("E:\\legacy\\Data.db");

        Assert.Equal("E:\\legacy\\Data.db", legacyImportService.LastPath);
        Assert.Equal(2, result.ImportedWearPartDefinitions);
        Assert.Equal(
            LocalizedText.Format("ViewModels.PartManagementVm.ImportedLegacyDefinitions", 2, 1, 0),
            viewModel.StatusMessage);
        Assert.True(uiDispatcher.RenderCount >= 2);
    }

    [Fact]
    public void ImportLegacyDefinitionsCommand_ShouldRaiseRequestedEvent()
    {
        var viewModel = new PartManagementViewModel(
            new StubClientAppInfoService(),
            new StubLegacyDatabaseImportService(),
            new StubWearPartManagementService(),
            CreateCurrentUserAccessor(4),
            new StubUiDispatcher(),
            new UiBusyService(TimeSpan.Zero),
            new StubAppDialogService());
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
            CreateCurrentUserAccessor(4),
            new StubUiDispatcher(),
            new UiBusyService(TimeSpan.Zero),
            new StubAppDialogService());

        using var _ = new TestCultureScope("en-US");
        LocalizationBindingSource.Instance.Refresh();

        Assert.Equal(LocalizedText.Get("ViewModels.PartManagementVm.PromptLoadCurrent"), viewModel.StatusMessage);
    }

    [Fact]
    public async Task DeleteCommand_WhenCheckedDefinitionsExist_ShouldDeleteAllCheckedDefinitions()
    {
        using var cultureScope = new TestCultureScope("zh-CN");
        var managementService = new StubWearPartManagementService(
            new WearPartDefinition { Id = Guid.NewGuid(), ResourceNumber = "RES-01", PartName = "刀具 A" },
            new WearPartDefinition { Id = Guid.NewGuid(), ResourceNumber = "RES-01", PartName = "刀具 B" },
            new WearPartDefinition { Id = Guid.NewGuid(), ResourceNumber = "RES-01", PartName = "刀具 C" });
        var viewModel = new PartManagementViewModel(
            new StubClientAppInfoService(),
            new StubLegacyDatabaseImportService(),
            managementService,
            CreateCurrentUserAccessor(4),
            new StubUiDispatcher(),
            new UiBusyService(TimeSpan.Zero),
            new StubAppDialogService());

        await viewModel.InitializeAsync();
        viewModel.Definitions[0].IsChecked = true;
        viewModel.Definitions[1].IsChecked = true;

        await viewModel.DeleteCommand.ExecuteAsync(null);

        Assert.Equal(2, managementService.DeletedIds.Count);
        Assert.Equal(LocalizedText.Format("ViewModels.PartManagementVm.DeletedMultiple", 2), viewModel.StatusMessage);
        Assert.Single(viewModel.Definitions);
    }

    [Fact]
    public async Task EditThresholdsCommand_WhenAccessLevelInsufficient_ShouldBeDisabled()
    {
        var definition = new WearPartDefinition { Id = Guid.NewGuid(), ResourceNumber = "RES-01", PartName = "刀具 A" };
        var viewModel = new PartManagementViewModel(
            new StubClientAppInfoService(),
            new StubLegacyDatabaseImportService(),
            new StubWearPartManagementService(definition),
            CreateCurrentUserAccessor(3),
            new StubUiDispatcher(),
            new UiBusyService(TimeSpan.Zero),
            new StubAppDialogService());

        await viewModel.InitializeAsync();
        viewModel.SelectedDefinition = viewModel.Definitions[0].Item;

        Assert.False(viewModel.EditThresholdsCommand.CanExecute(null));
    }

    [Fact]
    public async Task EditThresholdsCommand_WhenAccessLevelEligible_ShouldRaiseRequestedEvent()
    {
        var definition = new WearPartDefinition { Id = Guid.NewGuid(), ResourceNumber = "RES-01", PartName = "刀具 A" };
        var viewModel = new PartManagementViewModel(
            new StubClientAppInfoService(),
            new StubLegacyDatabaseImportService(),
            new StubWearPartManagementService(definition),
            CreateCurrentUserAccessor(4),
            new StubUiDispatcher(),
            new UiBusyService(TimeSpan.Zero),
            new StubAppDialogService());
        WearPartDefinition? requested = null;
        viewModel.ThresholdEditRequested += (_, item) => requested = item;

        await viewModel.InitializeAsync();
        viewModel.SelectedDefinition = viewModel.Definitions[0].Item;

        viewModel.EditThresholdsCommand.Execute(null);

        Assert.Same(viewModel.SelectedDefinition, requested);
    }

    private static CurrentUserAccessor CreateCurrentUserAccessor(int accessLevel)
    {
        var accessor = new CurrentUserAccessor();
        accessor.SetCurrentUser(new MhrUser
        {
            WorkId = "WORK-TEST",
            CardId = "CARD-TEST",
            AccessLevel = accessLevel
        });
        return accessor;
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
        private readonly List<WearPartDefinition> _definitions = [];

        public StubWearPartManagementService(params WearPartDefinition[] definitions)
        {
            _definitions.AddRange(definitions);
        }

        public List<Guid> DeletedIds { get; } = [];

        public Task<IReadOnlyList<WearPartDefinition>> GetDefinitionsByClientAppConfigurationAsync(Guid clientAppConfigurationId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<WearPartDefinition>>(_definitions.ToArray());
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
            DeletedIds.Add(id);
            _definitions.RemoveAll(x => x.Id == id);
            return Task.CompletedTask;
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

    private sealed class StubAppDialogService : IAppDialogService
    {
        public bool ShowDialog(System.Windows.Window dialog, System.Windows.Window? owner = null)
        {
            throw new NotSupportedException();
        }

        public System.Windows.MessageBoxResult ShowMessage(string message, string title, System.Windows.MessageBoxButton buttons = System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage image = System.Windows.MessageBoxImage.None, System.Windows.Window? owner = null, System.Windows.MessageBoxResult defaultResult = System.Windows.MessageBoxResult.None)
        {
            return System.Windows.MessageBoxResult.Yes;
        }
    }
}