using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WearPartsControl.ApplicationServices;
using WearPartsControl.ApplicationServices.Localization;
using WearPartsControl.ApplicationServices.PartServices;

namespace WearPartsControl.ViewModels;

public sealed class ToolChangeManagementViewModel : ObservableObject
{
    private readonly IToolChangeManagementService _toolChangeManagementService;
    private readonly IUiBusyService _uiBusyService;
    private readonly List<ToolChangeDefinition> _allDefinitions = new();
    private ToolChangeDefinition? _selectedDefinition;
    private string _toolName = string.Empty;
    private string _toolCode = string.Empty;
    private string _keyword = string.Empty;
    private string _statusMessage = LocalizedText.Get("ViewModels.ToolChangeManagementVm.PromptLoadCurrent");
    private bool _isBusy;
    private bool _isInitialized;

    public ToolChangeManagementViewModel(IToolChangeManagementService toolChangeManagementService, IUiBusyService uiBusyService)
    {
        _toolChangeManagementService = toolChangeManagementService;
        _uiBusyService = uiBusyService;

        SearchCommand = new RelayCommand(ApplyFilter);
        RefreshCommand = new AsyncRelayCommand(() => RefreshAsync(CancellationToken.None), () => !IsBusy);
        NewCommand = new RelayCommand(CreateNew, () => !IsBusy);
        SaveCommand = new AsyncRelayCommand(() => SaveAsync(CancellationToken.None), CanSave);
        DeleteCommand = new AsyncRelayCommand(() => DeleteAsync(CancellationToken.None), CanDelete);
    }

    public ObservableCollection<ToolChangeDefinition> Definitions { get; } = new();

    public IRelayCommand SearchCommand { get; }

    public IAsyncRelayCommand RefreshCommand { get; }

    public IRelayCommand NewCommand { get; }

    public IAsyncRelayCommand SaveCommand { get; }

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
                }

                SaveCommand.NotifyCanExecuteChanged();
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
                SaveCommand.NotifyCanExecuteChanged();
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
                SaveCommand.NotifyCanExecuteChanged();
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
                SaveCommand.NotifyCanExecuteChanged();
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
        StatusMessage = LocalizedText.Get("ViewModels.ToolChangeManagementVm.Loading");
        using var _ = _uiBusyService.Enter(StatusMessage);

        try
        {
            await ReloadDefinitionsAsync(cancellationToken, preserveSelection: true).ConfigureAwait(true);

            StatusMessage = _allDefinitions.Count == 0
                ? LocalizedText.Get("ViewModels.ToolChangeManagementVm.Empty")
                : LocalizedText.Format("ViewModels.ToolChangeManagementVm.Loaded", _allDefinitions.Count);
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        if (!CanSave())
        {
            return;
        }

        IsBusy = true;
        StatusMessage = LocalizedText.Get("ViewModels.ToolChangeManagementVm.Saving");
        using var _ = _uiBusyService.Enter(StatusMessage);

        try
        {
            var isCreating = SelectedDefinition is null;
            var model = new ToolChangeDefinition
            {
                Id = SelectedDefinition?.Id ?? Guid.Empty,
                Name = ToolName,
                Code = ToolCode
            };

            var saved = SelectedDefinition is null
                ? await _toolChangeManagementService.CreateAsync(model, cancellationToken).ConfigureAwait(true)
                : await _toolChangeManagementService.UpdateAsync(model, cancellationToken).ConfigureAwait(true);

            if (isCreating && !string.IsNullOrWhiteSpace(Keyword))
            {
                Keyword = string.Empty;
            }

            await ReloadDefinitionsAsync(cancellationToken, saved.Id).ConfigureAwait(true);
            SelectedDefinition = Definitions.FirstOrDefault(x => x.Id == saved.Id);
            StatusMessage = SelectedDefinition is null
                ? LocalizedText.Get("ViewModels.ToolChangeManagementVm.Saved")
                : LocalizedText.Format("ViewModels.ToolChangeManagementVm.SavedWithName", saved.Name);
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
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
        var result = MessageBox.Show(
            LocalizedText.Format("ViewModels.ToolChangeManagementVm.DeleteConfirmationMessage", selected.Name),
            LocalizedText.Get("ViewModels.ToolChangeManagementVm.DeleteConfirmationTitle"),
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        IsBusy = true;
        StatusMessage = LocalizedText.Get("ViewModels.ToolChangeManagementVm.Deleting");
        using var _ = _uiBusyService.Enter(StatusMessage);

        try
        {
            await _toolChangeManagementService.DeleteAsync(selected.Id, cancellationToken).ConfigureAwait(true);
            await ReloadDefinitionsAsync(cancellationToken, selectedId: null).ConfigureAwait(true);
            CreateNew();
            StatusMessage = LocalizedText.Format("ViewModels.ToolChangeManagementVm.Deleted", selected.Name);
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void CreateNew()
    {
        SelectedDefinition = null;
        ToolName = string.Empty;
        ToolCode = string.Empty;
        StatusMessage = LocalizedText.Get("ViewModels.ToolChangeManagementVm.Creating");
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

    private bool CanSave()
    {
        return !IsBusy && !string.IsNullOrWhiteSpace(ToolName) && !string.IsNullOrWhiteSpace(ToolCode);
    }

    private bool CanDelete()
    {
        return !IsBusy && SelectedDefinition is not null;
    }
}