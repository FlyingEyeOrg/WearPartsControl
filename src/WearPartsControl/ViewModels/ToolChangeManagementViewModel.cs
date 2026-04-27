using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using CommunityToolkit.Mvvm.Input;
using WearPartsControl.ApplicationServices;
using WearPartsControl.ApplicationServices.Dialogs;
using WearPartsControl.ApplicationServices.Localization;
using WearPartsControl.ApplicationServices.PartServices;

namespace WearPartsControl.ViewModels;

public sealed class ToolChangeManagementViewModel : LocalizedViewModelBase
{
    private readonly IToolChangeManagementService _toolChangeManagementService;
    private readonly IUiDispatcher _uiDispatcher;
    private readonly IUiBusyService _uiBusyService;
    private readonly IAppDialogService _dialogService;
    private readonly List<SelectableItem<ToolChangeDefinition>> _allDefinitions = new();
    private SelectableItem<ToolChangeDefinition>? _selectedDefinitionRow;
    private string _toolName = string.Empty;
    private string _toolCode = string.Empty;
    private string _keyword = string.Empty;
    private string _statusMessage = LocalizedText.Get("ViewModels.ToolChangeManagementVm.PromptLoadCurrent");
    private bool _areAllDefinitionsChecked;
    private bool _isBusy;
    private bool _isInitialized;
    private bool _isUpdatingCheckedState;
    private Func<string>? _statusMessageFactory;

    public ToolChangeManagementViewModel(IToolChangeManagementService toolChangeManagementService, IUiDispatcher uiDispatcher, IUiBusyService uiBusyService, IAppDialogService dialogService)
    {
        _toolChangeManagementService = toolChangeManagementService;
        _uiDispatcher = uiDispatcher;
        _uiBusyService = uiBusyService;
        _dialogService = dialogService;

        SearchCommand = new RelayCommand(ApplyFilter);
        RefreshCommand = new AsyncRelayCommand(() => RefreshAsync(CancellationToken.None), () => !IsBusy);
        NewCommand = new AsyncRelayCommand(() => CreateAsync(CancellationToken.None), CanCreate);
        EditCommand = new AsyncRelayCommand(() => EditAsync(CancellationToken.None), CanEdit);
        DeleteCommand = new AsyncRelayCommand(() => DeleteAsync(CancellationToken.None), CanDelete);
        SetLocalizedStatusMessage(() => LocalizedText.Get("ViewModels.ToolChangeManagementVm.PromptLoadCurrent"));
    }

    public ObservableCollection<SelectableItem<ToolChangeDefinition>> Definitions { get; } = new();

    public IRelayCommand SearchCommand { get; }

    public IAsyncRelayCommand RefreshCommand { get; }

    public IAsyncRelayCommand NewCommand { get; }

    public IAsyncRelayCommand EditCommand { get; }

    public IAsyncRelayCommand DeleteCommand { get; }

    public SelectableItem<ToolChangeDefinition>? SelectedDefinitionRow
    {
        get => _selectedDefinitionRow;
        set
        {
            if (!SetProperty(ref _selectedDefinitionRow, value))
            {
                return;
            }

            OnPropertyChanged(nameof(SelectedDefinition));

            var definition = value?.Item;
            if (definition is null)
            {
                ToolName = string.Empty;
                ToolCode = string.Empty;
            }
            else
            {
                ToolName = definition.Name;
                ToolCode = definition.Code;
                SetLocalizedStatusMessage(() => LocalizedText.Format("ViewModels.ToolChangeManagementVm.Editing", definition.Name));
            }

            NewCommand.NotifyCanExecuteChanged();
            EditCommand.NotifyCanExecuteChanged();
            DeleteCommand.NotifyCanExecuteChanged();
        }
    }

    public ToolChangeDefinition? SelectedDefinition
    {
        get => SelectedDefinitionRow?.Item;
        set => SelectedDefinitionRow = value is null ? null : Definitions.FirstOrDefault(x => x.Item.Id == value.Id);
    }

    public bool AreAllDefinitionsChecked
    {
        get => _areAllDefinitionsChecked;
        set
        {
            if (!SetProperty(ref _areAllDefinitionsChecked, value))
            {
                return;
            }

            if (_isUpdatingCheckedState)
            {
                return;
            }

            foreach (var definition in Definitions)
            {
                definition.IsChecked = value;
            }

            DeleteCommand.NotifyCanExecuteChanged();
        }
    }

    public string ToolName
    {
        get => _toolName;
        set
        {
            if (SetProperty(ref _toolName, value))
            {
                NewCommand.NotifyCanExecuteChanged();
                EditCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string ToolCode
    {
        get => _toolCode;
        set
        {
            if (SetProperty(ref _toolCode, value))
            {
                NewCommand.NotifyCanExecuteChanged();
                EditCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string Keyword
    {
        get => _keyword;
        set => SetProperty(ref _keyword, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                RefreshCommand.NotifyCanExecuteChanged();
                NewCommand.NotifyCanExecuteChanged();
                EditCommand.NotifyCanExecuteChanged();
                DeleteCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_isInitialized)
        {
            return;
        }

        await RefreshAsync(cancellationToken).ConfigureAwait(true);
        _isInitialized = true;
    }

    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        SetLocalizedStatusMessage(() => LocalizedText.Get("ViewModels.ToolChangeManagementVm.Loading"));
        using var _ = _uiBusyService.Enter(StatusMessage);
        await _uiDispatcher.RenderAsync().ConfigureAwait(true);

        try
        {
            await ReloadDefinitionsAsync(cancellationToken, preserveSelection: true).ConfigureAwait(true);

            SetLocalizedStatusMessage(() => _allDefinitions.Count == 0
                ? LocalizedText.Get("ViewModels.ToolChangeManagementVm.Empty")
                : LocalizedText.Format("ViewModels.ToolChangeManagementVm.Loaded", _allDefinitions.Count));
        }
        catch (Exception ex)
        {
            SetRawStatusMessage(ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task CreateAsync(CancellationToken cancellationToken)
    {
        if (!CanCreate())
        {
            return;
        }

        IsBusy = true;
        SetLocalizedStatusMessage(() => LocalizedText.Get("ViewModels.ToolChangeManagementVm.CreatingOperation"));
        using var _ = _uiBusyService.Enter(StatusMessage);
        await _uiDispatcher.RenderAsync().ConfigureAwait(true);

        try
        {
            var model = new ToolChangeDefinition
            {
                Name = ToolName,
                Code = ToolCode
            };

            var saved = await _toolChangeManagementService.CreateAsync(model, cancellationToken).ConfigureAwait(true);

            if (!string.IsNullOrWhiteSpace(Keyword))
            {
                Keyword = string.Empty;
            }

            await ReloadDefinitionsAsync(cancellationToken, saved.Id).ConfigureAwait(true);
            SelectedDefinition = Definitions.FirstOrDefault(x => x.Item.Id == saved.Id)?.Item;
            SetLocalizedStatusMessage(() => LocalizedText.Format("ViewModels.ToolChangeManagementVm.CreatedWithName", saved.Name));
        }
        catch (Exception ex)
        {
            SetRawStatusMessage(ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task EditAsync(CancellationToken cancellationToken)
    {
        if (!CanEdit())
        {
            return;
        }

        IsBusy = true;
        SetLocalizedStatusMessage(() => LocalizedText.Get("ViewModels.ToolChangeManagementVm.Updating"));
        using var _ = _uiBusyService.Enter(StatusMessage);
        await _uiDispatcher.RenderAsync().ConfigureAwait(true);

        try
        {
            var model = new ToolChangeDefinition
            {
                Id = SelectedDefinition!.Id,
                Name = ToolName,
                Code = ToolCode
            };

            var saved = await _toolChangeManagementService.UpdateAsync(model, cancellationToken).ConfigureAwait(true);
            await ReloadDefinitionsAsync(cancellationToken, saved.Id).ConfigureAwait(true);
            SelectedDefinition = Definitions.FirstOrDefault(x => x.Item.Id == saved.Id)?.Item;
            SetLocalizedStatusMessage(() => LocalizedText.Format("ViewModels.ToolChangeManagementVm.UpdatedWithName", saved.Name));
        }
        catch (Exception ex)
        {
            SetRawStatusMessage(ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task DeleteAsync(CancellationToken cancellationToken)
    {
        var definitionsToDelete = Definitions.Where(x => x.IsChecked).Select(x => x.Item).ToList();
        if (definitionsToDelete.Count == 0 && SelectedDefinition is not null)
        {
            definitionsToDelete.Add(SelectedDefinition);
        }

        if (definitionsToDelete.Count == 0)
        {
            return;
        }

        var isBatchDelete = definitionsToDelete.Count > 1;
        var result = _dialogService.ShowMessage(
            isBatchDelete
                ? LocalizedText.Format("ViewModels.ToolChangeManagementVm.DeleteMultipleConfirmationMessage", definitionsToDelete.Count)
                : LocalizedText.Format("ViewModels.ToolChangeManagementVm.DeleteConfirmationMessage", definitionsToDelete[0].Name),
            LocalizedText.Get("ViewModels.ToolChangeManagementVm.DeleteConfirmationTitle"),
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        IsBusy = true;
        SetLocalizedStatusMessage(() => LocalizedText.Get("ViewModels.ToolChangeManagementVm.Deleting"));
        using var _ = _uiBusyService.Enter(StatusMessage);
        await _uiDispatcher.RenderAsync().ConfigureAwait(true);

        try
        {
            foreach (var definition in definitionsToDelete)
            {
                await _toolChangeManagementService.DeleteAsync(definition.Id, cancellationToken).ConfigureAwait(true);
            }

            await ReloadDefinitionsAsync(cancellationToken, selectedId: null).ConfigureAwait(true);
            ClearEditor();
            SetLocalizedStatusMessage(() => isBatchDelete
                ? LocalizedText.Format("ViewModels.ToolChangeManagementVm.DeletedMultiple", definitionsToDelete.Count)
                : LocalizedText.Format("ViewModels.ToolChangeManagementVm.Deleted", definitionsToDelete[0].Name));
        }
        catch (Exception ex)
        {
            SetRawStatusMessage(ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ClearEditor()
    {
        SelectedDefinition = null;
        ToolName = string.Empty;
        ToolCode = string.Empty;
    }

    private void ApplyFilter()
    {
        var keyword = Keyword?.Trim();
        var filtered = string.IsNullOrWhiteSpace(keyword)
            ? _allDefinitions
            : _allDefinitions.Where(x => x.Item.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase) || x.Item.Code.Contains(keyword, StringComparison.OrdinalIgnoreCase)).ToList();

        Definitions.Clear();
        foreach (var definition in filtered)
        {
            Definitions.Add(definition);
        }

        if (SelectedDefinition is not null)
        {
            SelectedDefinition = Definitions.FirstOrDefault(x => x.Item.Id == SelectedDefinition.Id)?.Item;
        }

        UpdateCheckedSummaryState();
        DeleteCommand.NotifyCanExecuteChanged();
    }

    private async Task ReloadDefinitionsAsync(CancellationToken cancellationToken, bool preserveSelection)
    {
        await ReloadDefinitionsAsync(cancellationToken, preserveSelection ? SelectedDefinition?.Id : null).ConfigureAwait(true);
    }

    private async Task ReloadDefinitionsAsync(CancellationToken cancellationToken, Guid? selectedId)
    {
        var definitions = await _toolChangeManagementService.GetAllAsync(cancellationToken).ConfigureAwait(true);
        ClearDefinitionRows();
        foreach (var definition in definitions)
        {
            var row = new SelectableItem<ToolChangeDefinition>(definition);
            row.PropertyChanged += OnDefinitionRowPropertyChanged;
            _allDefinitions.Add(row);
        }

        ApplyFilter();

        if (selectedId.HasValue)
        {
            SelectedDefinition = Definitions.FirstOrDefault(x => x.Item.Id == selectedId.Value)?.Item;
        }
    }

    private bool CanCreate()
    {
        return !IsBusy && !string.IsNullOrWhiteSpace(ToolName) && !string.IsNullOrWhiteSpace(ToolCode);
    }

    private bool CanEdit()
    {
        return !IsBusy && SelectedDefinition is not null && !string.IsNullOrWhiteSpace(ToolName) && !string.IsNullOrWhiteSpace(ToolCode);
    }

    private bool CanDelete()
    {
        return !IsBusy && (SelectedDefinition is not null || Definitions.Any(x => x.IsChecked));
    }

    protected override void OnLocalizationRefreshed()
    {
        if (_statusMessageFactory is not null)
        {
            StatusMessage = _statusMessageFactory();
        }
    }

    private void SetLocalizedStatusMessage(Func<string> factory)
    {
        _statusMessageFactory = factory;
        StatusMessage = factory();
    }

    private void SetRawStatusMessage(string message)
    {
        _statusMessageFactory = null;
        StatusMessage = message;
    }

    private void OnDefinitionRowPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(SelectableItem<ToolChangeDefinition>.IsChecked))
        {
            return;
        }

        UpdateCheckedSummaryState();
        DeleteCommand.NotifyCanExecuteChanged();
    }

    private void UpdateCheckedSummaryState()
    {
        _isUpdatingCheckedState = true;
        SetProperty(ref _areAllDefinitionsChecked, Definitions.Count > 0 && Definitions.All(x => x.IsChecked), nameof(AreAllDefinitionsChecked));
        _isUpdatingCheckedState = false;
    }

    private void ClearDefinitionRows()
    {
        foreach (var definition in _allDefinitions)
        {
            definition.PropertyChanged -= OnDefinitionRowPropertyChanged;
        }

        _allDefinitions.Clear();
    }
}