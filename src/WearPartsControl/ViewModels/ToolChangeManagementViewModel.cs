using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
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
    private readonly List<ToolChangeDefinition> _allDefinitions = new();
    private ToolChangeDefinition? _selectedDefinition;
    private string _toolName = string.Empty;
    private string _toolCode = string.Empty;
    private string _keyword = string.Empty;
    private string _statusMessage = LocalizedText.Get("ViewModels.ToolChangeManagementVm.PromptLoadCurrent");
    private bool _isBusy;
    private bool _isInitialized;
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

    public ObservableCollection<ToolChangeDefinition> Definitions { get; } = new();

    public IRelayCommand SearchCommand { get; }

    public IAsyncRelayCommand RefreshCommand { get; }

    public IAsyncRelayCommand NewCommand { get; }

    public IAsyncRelayCommand EditCommand { get; }

    public IAsyncRelayCommand DeleteCommand { get; }

    public ToolChangeDefinition? SelectedDefinition
    {
        get => _selectedDefinition;
        set
        {
            if (SetProperty(ref _selectedDefinition, value))
            {
                if (value is null)
                {
                    ToolName = string.Empty;
                    ToolCode = string.Empty;
                }
                else
                {
                    ToolName = value.Name;
                    ToolCode = value.Code;
                    SetLocalizedStatusMessage(() => LocalizedText.Format("ViewModels.ToolChangeManagementVm.Editing", value.Name));
                }

                NewCommand.NotifyCanExecuteChanged();
                EditCommand.NotifyCanExecuteChanged();
                DeleteCommand.NotifyCanExecuteChanged();
            }
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
            SelectedDefinition = Definitions.FirstOrDefault(x => x.Id == saved.Id);
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
            SelectedDefinition = Definitions.FirstOrDefault(x => x.Id == saved.Id);
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
        if (SelectedDefinition is null)
        {
            return;
        }

        var selected = SelectedDefinition;
        var result = _dialogService.ShowMessage(
            LocalizedText.Format("ViewModels.ToolChangeManagementVm.DeleteConfirmationMessage", selected.Name),
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
            await _toolChangeManagementService.DeleteAsync(selected.Id, cancellationToken).ConfigureAwait(true);
            await ReloadDefinitionsAsync(cancellationToken, selectedId: null).ConfigureAwait(true);
            ClearEditor();
            SetLocalizedStatusMessage(() => LocalizedText.Format("ViewModels.ToolChangeManagementVm.Deleted", selected.Name));
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
            : _allDefinitions.Where(x => x.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase) || x.Code.Contains(keyword, StringComparison.OrdinalIgnoreCase)).ToList();

        Definitions.Clear();
        foreach (var definition in filtered)
        {
            Definitions.Add(definition);
        }

        if (SelectedDefinition is not null)
        {
            SelectedDefinition = Definitions.FirstOrDefault(x => x.Id == SelectedDefinition.Id);
        }
    }

    private async Task ReloadDefinitionsAsync(CancellationToken cancellationToken, bool preserveSelection)
    {
        await ReloadDefinitionsAsync(cancellationToken, preserveSelection ? SelectedDefinition?.Id : null).ConfigureAwait(true);
    }

    private async Task ReloadDefinitionsAsync(CancellationToken cancellationToken, Guid? selectedId)
    {
        var definitions = await _toolChangeManagementService.GetAllAsync(cancellationToken).ConfigureAwait(true);
        _allDefinitions.Clear();
        _allDefinitions.AddRange(definitions);
        ApplyFilter();

        if (selectedId.HasValue)
        {
            SelectedDefinition = Definitions.FirstOrDefault(x => x.Id == selectedId.Value);
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
        return !IsBusy && SelectedDefinition is not null;
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
}