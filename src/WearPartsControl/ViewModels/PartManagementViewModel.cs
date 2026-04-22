using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WearPartsControl.ApplicationServices;
using WearPartsControl.ApplicationServices.ClientAppInfo;
using WearPartsControl.ApplicationServices.LegacyImport;
using WearPartsControl.ApplicationServices.Localization;
using WearPartsControl.ApplicationServices.PartServices;

namespace WearPartsControl.ViewModels;

public sealed class PartManagementViewModel : ObservableObject
{
    private readonly IClientAppInfoService _clientAppInfoService;
    private readonly ILegacyDatabaseImportService _legacyDatabaseImportService;
    private readonly IWearPartManagementService _wearPartManagementService;
    private readonly IUiBusyService _uiBusyService;
    private readonly List<WearPartDefinition> _allDefinitions = new();
    private Guid _clientAppConfigurationId;
    private string _resourceNumber = string.Empty;
    private string _partNameFilter = string.Empty;
    private WearPartDefinition? _selectedDefinition;
    private bool _isBusy;
    private bool _isInitialized;
    private string _statusMessage = LocalizedText.Get("ViewModels.PartManagementVm.PromptLoadCurrent");

    public PartManagementViewModel(
        IClientAppInfoService clientAppInfoService,
        ILegacyDatabaseImportService legacyDatabaseImportService,
        IWearPartManagementService wearPartManagementService,
        IUiBusyService uiBusyService)
    {
        _clientAppInfoService = clientAppInfoService;
        _legacyDatabaseImportService = legacyDatabaseImportService;
        _wearPartManagementService = wearPartManagementService;
        _uiBusyService = uiBusyService;

        SearchCommand = new RelayCommand(ApplyFilter);
        RefreshCommand = new AsyncRelayCommand(RefreshCommandAsync, CanRefresh);
        ImportLegacyDefinitionsCommand = new RelayCommand(RequestImportLegacyDefinitions, CanImportLegacyDefinitions);
        AddCommand = new RelayCommand(OpenAddDialog, CanAdd);
        EditCommand = new RelayCommand(OpenEditDialog, CanEdit);
        DeleteCommand = new AsyncRelayCommand(DeleteAsync, CanDelete);
    }

    public event EventHandler? AddRequested;

    public event EventHandler? ImportLegacyDefinitionsRequested;

    public event EventHandler<WearPartDefinition>? EditRequested;

    public ObservableCollection<WearPartDefinition> Definitions { get; } = new();

    public IRelayCommand SearchCommand { get; }

    public IAsyncRelayCommand RefreshCommand { get; }

    public IRelayCommand ImportLegacyDefinitionsCommand { get; }

    public IRelayCommand AddCommand { get; }

    public IRelayCommand EditCommand { get; }

    public IAsyncRelayCommand DeleteCommand { get; }

    public Guid ClientAppConfigurationId => _clientAppConfigurationId;

    public string ResourceNumber
    {
        get => _resourceNumber;
        private set => SetProperty(ref _resourceNumber, value);
    }

    public string PartNameFilter
    {
        get => _partNameFilter;
        set => SetProperty(ref _partNameFilter, value);
    }

    public WearPartDefinition? SelectedDefinition
    {
        get => _selectedDefinition;
        set
        {
            if (SetProperty(ref _selectedDefinition, value))
            {
                EditCommand.NotifyCanExecuteChanged();
                DeleteCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(IsNotBusy));
                RefreshCommand.NotifyCanExecuteChanged();
                ImportLegacyDefinitionsCommand.NotifyCanExecuteChanged();
                AddCommand.NotifyCanExecuteChanged();
                EditCommand.NotifyCanExecuteChanged();
                DeleteCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool IsNotBusy => !IsBusy;

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
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

    public Task RefreshAsync()
    {
        return RefreshAsync(CancellationToken.None);
    }

    private Task RefreshCommandAsync()
    {
        return RefreshAsync();
    }

    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        var enteredAt = DateTimeOffset.UtcNow;
        IsBusy = true;
        StatusMessage = LocalizedText.Get("ViewModels.PartManagementVm.Loading");
        using var _ = _uiBusyService.Enter(LocalizedText.Get("ViewModels.PartManagementVm.Loading"));

        try
        {
            var clientInfo = await _clientAppInfoService.GetAsync(cancellationToken).ConfigureAwait(true);
            _clientAppConfigurationId = clientInfo.Id ?? Guid.Empty;
            ResourceNumber = clientInfo.ResourceNumber?.Trim() ?? string.Empty;

            _allDefinitions.Clear();
            Definitions.Clear();
            SelectedDefinition = null;

            if (_clientAppConfigurationId == Guid.Empty || string.IsNullOrWhiteSpace(ResourceNumber))
            {
                StatusMessage = LocalizedText.Get("ViewModels.PartManagementVm.ResourceNumberMissing");
                return;
            }

            var definitions = await _wearPartManagementService
                .GetDefinitionsByClientAppConfigurationAsync(_clientAppConfigurationId, cancellationToken)
                .ConfigureAwait(true);

            _allDefinitions.AddRange(definitions.OrderBy(x => x.PartName, StringComparer.OrdinalIgnoreCase));
            ApplyFilter();
            await EnsureMinimumBusyDurationAsync(enteredAt, cancellationToken).ConfigureAwait(true);
            StatusMessage = _allDefinitions.Count == 0
                ? LocalizedText.Format("ViewModels.PartManagementVm.DefinitionsEmpty", ResourceNumber)
                : LocalizedText.Format("ViewModels.PartManagementVm.DefinitionsLoaded", ResourceNumber, _allDefinitions.Count);
        }
        catch (Exception ex)
        {
            await EnsureMinimumBusyDurationAsync(enteredAt, cancellationToken).ConfigureAwait(true);
            StatusMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanRefresh()
    {
        return !IsBusy;
    }

    private bool CanAdd()
    {
        return !IsBusy && _clientAppConfigurationId != Guid.Empty && !string.IsNullOrWhiteSpace(ResourceNumber);
    }

    private bool CanImportLegacyDefinitions()
    {
        return !IsBusy && _clientAppConfigurationId != Guid.Empty && !string.IsNullOrWhiteSpace(ResourceNumber);
    }

    private bool CanEdit()
    {
        return !IsBusy && SelectedDefinition is not null;
    }

    private bool CanDelete()
    {
        return !IsBusy && SelectedDefinition is not null;
    }

    private void OpenAddDialog()
    {
        AddRequested?.Invoke(this, EventArgs.Empty);
    }

    private void RequestImportLegacyDefinitions()
    {
        ImportLegacyDefinitionsRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OpenEditDialog()
    {
        if (SelectedDefinition is null)
        {
            return;
        }

        EditRequested?.Invoke(this, SelectedDefinition);
    }

    private async Task DeleteAsync()
    {
        if (SelectedDefinition is null)
        {
            return;
        }

        var definition = SelectedDefinition;
        var result = MessageBox.Show(
            LocalizedText.Format("ViewModels.PartManagementVm.DeleteConfirmationMessage", definition.PartName),
            LocalizedText.Get("ViewModels.PartManagementVm.DeleteConfirmationTitle"),
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        var enteredAt = DateTimeOffset.UtcNow;
        IsBusy = true;
        StatusMessage = LocalizedText.Get("ViewModels.PartManagementVm.Deleting");
        using var _ = _uiBusyService.Enter(LocalizedText.Get("ViewModels.PartManagementVm.Deleting"));

        try
        {
            await _wearPartManagementService.DeleteDefinitionAsync(definition.Id).ConfigureAwait(true);
            await RefreshAsync(CancellationToken.None).ConfigureAwait(true);
            await EnsureMinimumBusyDurationAsync(enteredAt, CancellationToken.None).ConfigureAwait(true);
            StatusMessage = LocalizedText.Format("ViewModels.PartManagementVm.Deleted", definition.PartName);
        }
        catch (Exception ex)
        {
            await EnsureMinimumBusyDurationAsync(enteredAt, CancellationToken.None).ConfigureAwait(true);
            StatusMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task<LegacyDatabaseImportResult> ImportLegacyDefinitionsAsync(string legacyDatabasePath, CancellationToken cancellationToken = default)
    {
        var enteredAt = DateTimeOffset.UtcNow;
        IsBusy = true;
        StatusMessage = LocalizedText.Get("ViewModels.PartManagementVm.ImportingLegacyDefinitions");
        using var _ = _uiBusyService.Enter(LocalizedText.Get("ViewModels.PartManagementVm.ImportingLegacyDefinitions"));

        try
        {
            var result = await _legacyDatabaseImportService
                .ImportWearPartDefinitionsAsync(legacyDatabasePath, _clientAppConfigurationId, ResourceNumber, cancellationToken)
                .ConfigureAwait(true);

            await RefreshAsync(cancellationToken).ConfigureAwait(true);
            await EnsureMinimumBusyDurationAsync(enteredAt, cancellationToken).ConfigureAwait(true);
            StatusMessage = LocalizedText.Format(
                "ViewModels.PartManagementVm.ImportedLegacyDefinitions",
                result.ImportedWearPartDefinitions,
                result.UpdatedWearPartDefinitions,
                result.SkippedRows);
            return result;
        }
        catch (Exception ex)
        {
            await EnsureMinimumBusyDurationAsync(enteredAt, cancellationToken).ConfigureAwait(true);
            StatusMessage = ex.Message;
            throw;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void NotifyLegacyImportCanceled()
    {
        StatusMessage = LocalizedText.Get("ViewModels.PartManagementVm.ImportCanceled");
    }

    private void ApplyFilter()
    {
        var keyword = PartNameFilter?.Trim();
        var filtered = string.IsNullOrWhiteSpace(keyword)
            ? _allDefinitions
            : _allDefinitions.Where(x => x.PartName.Contains(keyword, StringComparison.OrdinalIgnoreCase)).ToList();

        Definitions.Clear();
        foreach (var definition in filtered)
        {
            Definitions.Add(definition);
        }

        if (SelectedDefinition is not null && !Definitions.Any(x => x.Id == SelectedDefinition.Id))
        {
            SelectedDefinition = null;
        }

        if (!string.IsNullOrWhiteSpace(ResourceNumber))
        {
            StatusMessage = Definitions.Count == 0
                ? LocalizedText.Format("ViewModels.PartManagementVm.NoMatchedDefinitions", ResourceNumber, keyword ?? string.Empty)
                : LocalizedText.Format("ViewModels.PartManagementVm.FilteredDefinitionsCount", Definitions.Count);
        }
    }

    private async Task EnsureMinimumBusyDurationAsync(DateTimeOffset busyEnteredAt, CancellationToken cancellationToken)
    {
        var elapsed = DateTimeOffset.UtcNow - busyEnteredAt;
        var remaining = _uiBusyService.MinimumBusyDuration - elapsed;
        if (remaining <= TimeSpan.Zero)
        {
            return;
        }

        await Task.Delay(remaining, cancellationToken).ConfigureAwait(true);
    }
}