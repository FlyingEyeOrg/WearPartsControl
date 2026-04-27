using WearPartsControl.ApplicationServices;
using WearPartsControl.ApplicationServices.Dialogs;
using WearPartsControl.ApplicationServices.Localization;
using WearPartsControl.ApplicationServices.PartServices;
using WearPartsControl.ViewModels;
using Xunit;

namespace WearPartsControl.Tests;

[Collection(LocalizationSensitiveTestCollection.Name)]
public sealed class ToolChangeManagementViewModelTests
{
    [Fact]
    public async Task NewCommand_WhenCreatingDefinition_ShouldRefreshDefinitions()
    {
        var service = new StubToolChangeManagementService();
        var uiDispatcher = new StubUiDispatcher();
        var viewModel = new ToolChangeManagementViewModel(service, uiDispatcher, new UiBusyService(TimeSpan.Zero), new StubAppDialogService());

        await viewModel.InitializeAsync();
        viewModel.ToolName = "标准刀";
        viewModel.ToolCode = "TL-01";

        await viewModel.NewCommand.ExecuteAsync(null);

        Assert.Single(viewModel.Definitions);
        Assert.Equal("标准刀", viewModel.Definitions[0].Item.Name);
        Assert.Equal("TL-01", viewModel.Definitions[0].Item.Code);
        Assert.Same(viewModel.Definitions[0].Item, viewModel.SelectedDefinition);
        Assert.Equal(LocalizedText.Format("ViewModels.ToolChangeManagementVm.CreatedWithName", "标准刀"), viewModel.StatusMessage);
        Assert.Equal(1, service.CreateCount);
        Assert.True(uiDispatcher.RenderCount >= 2);
    }

    [Fact]
    public async Task EditCommand_WhenUpdatingSelectedDefinition_ShouldPersistChanges()
    {
        var existing = new ToolChangeDefinition
        {
            Id = Guid.NewGuid(),
            Name = "标准刀",
            Code = "TL-01"
        };

        var service = new StubToolChangeManagementService(existing);
        var uiDispatcher = new StubUiDispatcher();
        var viewModel = new ToolChangeManagementViewModel(service, uiDispatcher, new UiBusyService(TimeSpan.Zero), new StubAppDialogService());

        await viewModel.InitializeAsync();
        viewModel.SelectedDefinition = viewModel.Definitions.Single().Item;
        viewModel.ToolName = "标准刀-改";
        viewModel.ToolCode = "TL-02";

        await viewModel.EditCommand.ExecuteAsync(null);

        Assert.Equal(1, service.UpdateCount);
        Assert.Equal("标准刀-改", viewModel.SelectedDefinition?.Name);
        Assert.Equal("TL-02", viewModel.SelectedDefinition?.Code);
        Assert.Equal(LocalizedText.Format("ViewModels.ToolChangeManagementVm.UpdatedWithName", "标准刀-改"), viewModel.StatusMessage);
        Assert.True(uiDispatcher.RenderCount >= 2);
    }

    [Fact]
    public async Task LocalizationRefresh_ShouldUpdateEditingStatus()
    {
        using var cultureScope = new TestCultureScope("zh-CN");
        var existing = new ToolChangeDefinition
        {
            Id = Guid.NewGuid(),
            Name = "标准刀",
            Code = "TL-01"
        };
        var viewModel = new ToolChangeManagementViewModel(new StubToolChangeManagementService(existing), new StubUiDispatcher(), new UiBusyService(TimeSpan.Zero), new StubAppDialogService());

        await viewModel.InitializeAsync();
        viewModel.SelectedDefinition = viewModel.Definitions.Single().Item;

        using var _ = new TestCultureScope("en-US");
        LocalizationBindingSource.Instance.Refresh();

        Assert.Equal(LocalizedText.Format("ViewModels.ToolChangeManagementVm.Editing", "标准刀"), viewModel.StatusMessage);
    }

    [Fact]
    public async Task DeleteCommand_WhenCheckedDefinitionsExist_ShouldDeleteAllCheckedDefinitions()
    {
        using var cultureScope = new TestCultureScope("zh-CN");
        var service = new StubToolChangeManagementService(
            new ToolChangeDefinition { Id = Guid.NewGuid(), Name = "标准刀", Code = "TL-01" },
            new ToolChangeDefinition { Id = Guid.NewGuid(), Name = "修磨刀", Code = "TL-02" },
            new ToolChangeDefinition { Id = Guid.NewGuid(), Name = "备用刀", Code = "TL-03" });
        var viewModel = new ToolChangeManagementViewModel(service, new StubUiDispatcher(), new UiBusyService(TimeSpan.Zero), new StubAppDialogService());

        await viewModel.InitializeAsync();
        viewModel.Definitions[0].IsChecked = true;
        viewModel.Definitions[1].IsChecked = true;

        await viewModel.DeleteCommand.ExecuteAsync(null);

        Assert.Equal(2, service.DeletedIds.Count);
        Assert.Equal(LocalizedText.Format("ViewModels.ToolChangeManagementVm.DeletedMultiple", 2), viewModel.StatusMessage);
        Assert.Single(viewModel.Definitions);
    }

    private sealed class StubToolChangeManagementService : IToolChangeManagementService
    {
        private readonly List<ToolChangeDefinition> _definitions = [];

        public StubToolChangeManagementService(params ToolChangeDefinition[] definitions)
        {
            _definitions.AddRange(definitions);
        }

        public int CreateCount { get; private set; }

        public int UpdateCount { get; private set; }

        public List<Guid> DeletedIds { get; } = [];

        public Task<IReadOnlyList<ToolChangeDefinition>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<ToolChangeDefinition>>(_definitions.OrderBy(x => x.Name).ToArray());
        }

        public Task<ToolChangeDefinition> CreateAsync(ToolChangeDefinition definition, CancellationToken cancellationToken = default)
        {
            CreateCount++;
            var created = new ToolChangeDefinition
            {
                Id = Guid.NewGuid(),
                Name = definition.Name,
                Code = definition.Code
            };
            _definitions.Add(created);
            return Task.FromResult(created);
        }

        public Task<ToolChangeDefinition> UpdateAsync(ToolChangeDefinition definition, CancellationToken cancellationToken = default)
        {
            UpdateCount++;
            var index = _definitions.FindIndex(x => x.Id == definition.Id);
            if (index >= 0)
            {
                _definitions[index] = new ToolChangeDefinition
                {
                    Id = definition.Id,
                    Name = definition.Name,
                    Code = definition.Code
                };
            }

            return Task.FromResult(_definitions[index]);
        }

        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            DeletedIds.Add(id);
            _definitions.RemoveAll(x => x.Id == id);
            return Task.CompletedTask;
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