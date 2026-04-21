using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Media;
using WearPartsControl.ApplicationServices;
using WearPartsControl.ApplicationServices.AppSettings;
using WearPartsControl.ApplicationServices.Localization;
using WearPartsControl.ApplicationServices.PartServices;
using WearPartsControl.ApplicationServices.PlcService;

namespace WearPartsControl.ViewModels;

public sealed class ReplacePartViewModel : ObservableObject
{
    private readonly IAppSettingsService _appSettingsService;
    private readonly IWearPartManagementService _wearPartManagementService;
    private readonly IWearPartReplacementService _wearPartReplacementService;
    private readonly IUiBusyService _uiBusyService;
    private readonly IPlcConnectionStatusService _plcConnectionStatusService;
    private readonly List<WearPartDefinition> _allDefinitions = new();
    private string _plcConnectionStatusText = LocalizedText.Get("Services.PlcStartupConnection.Uninitialized");
    private Brush _plcConnectionStatusBackground = Brushes.Gray;
    private WearPartDefinition? _selectedDefinition;
    private string _resourceNumber = string.Empty;
    private string _inputMode = string.Empty;
    private string _codeMinLengthText = "0";
    private string _codeMaxLengthText = "0";
    private string _currentValue = "0";
    private string _warningValue = "0";
    private string _shutdownValue = "0";
    private string _lastBarcode = string.Empty;
    private string _newBarcode = string.Empty;
    private string _selectedReplacementReason = string.Empty;
    private string _replacementMessage = string.Empty;
    private string _statusMessage = LocalizedText.Get("ViewModels.ReplacePartVm.PromptSelectAndLoadPreview");
    private bool _isBusy;
    private bool _isInitialized;

    public ReplacePartViewModel(
        IAppSettingsService appSettingsService,
        IWearPartManagementService wearPartManagementService,
        IWearPartReplacementService wearPartReplacementService,
        IUiBusyService uiBusyService,
        IPlcConnectionStatusService plcConnectionStatusService)
    {
        _appSettingsService = appSettingsService;
        _wearPartManagementService = wearPartManagementService;
        _wearPartReplacementService = wearPartReplacementService;
        _uiBusyService = uiBusyService;
        _plcConnectionStatusService = plcConnectionStatusService;
        _plcConnectionStatusService.PropertyChanged += OnPlcConnectionStatusChanged;
        RefreshCommand = new AsyncRelayCommand(() => RefreshAsync(), CanRefresh);
        ReplaceCommand = new AsyncRelayCommand(ReplaceAsync, CanReplace);

        foreach (var reason in WearPartReplacementReason.All)
        {
            ReplacementReasons.Add(reason);
        }

        SelectedReplacementReason = ReplacementReasons.FirstOrDefault() ?? string.Empty;
        Apply(_plcConnectionStatusService.Current);
    }

    public ObservableCollection<WearPartDefinition> Definitions { get; } = new();

    public ObservableCollection<string> ReplacementReasons { get; } = new();

    public ObservableCollection<WearPartReplacementRecord> ReplacementHistory { get; } = new();

    public IAsyncRelayCommand RefreshCommand { get; }

    public IAsyncRelayCommand ReplaceCommand { get; }

    public string PlcConnectionStatusText
    {
        get => _plcConnectionStatusText;
        private set => SetProperty(ref _plcConnectionStatusText, value);
    }

    public Brush PlcConnectionStatusBackground
    {
        get => _plcConnectionStatusBackground;
        private set => SetProperty(ref _plcConnectionStatusBackground, value);
    }

    public string ResourceNumber
    {
        get => _resourceNumber;
        private set => SetProperty(ref _resourceNumber, value);
    }

    public WearPartDefinition? SelectedDefinition
    {
        get => _selectedDefinition;
        set
        {
            if (SetProperty(ref _selectedDefinition, value))
            {
                ApplySelectedDefinition(value);
                ReplaceCommand.NotifyCanExecuteChanged();
                _ = LoadPreviewAndHistoryAsync(CancellationToken.None);
            }
        }
    }

    public string InputMode
    {
        get => _inputMode;
        private set => SetProperty(ref _inputMode, value);
    }

    public string CodeMinLengthText
    {
        get => _codeMinLengthText;
        private set => SetProperty(ref _codeMinLengthText, value);
    }

    public string CodeMaxLengthText
    {
        get => _codeMaxLengthText;
        private set => SetProperty(ref _codeMaxLengthText, value);
    }

    public string CurrentValue
    {
        get => _currentValue;
        private set => SetProperty(ref _currentValue, value);
    }

    public string WarningValue
    {
        get => _warningValue;
        private set => SetProperty(ref _warningValue, value);
    }

    public string ShutdownValue
    {
        get => _shutdownValue;
        private set => SetProperty(ref _shutdownValue, value);
    }

    public string LastBarcode
    {
        get => _lastBarcode;
        private set => SetProperty(ref _lastBarcode, value);
    }

    public string NewBarcode
    {
        get => _newBarcode;
        set
        {
            if (SetProperty(ref _newBarcode, value))
            {
                ReplaceCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string SelectedReplacementReason
    {
        get => _selectedReplacementReason;
        set
        {
            if (SetProperty(ref _selectedReplacementReason, value))
            {
                ReplaceCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string ReplacementMessage
    {
        get => _replacementMessage;
        set => SetProperty(ref _replacementMessage, value);
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
                OnPropertyChanged(nameof(IsNotBusy));
                RefreshCommand.NotifyCanExecuteChanged();
                ReplaceCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool IsNotBusy => !IsBusy;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_isInitialized)
        {
            return;
        }

        await RefreshAsync(cancellationToken).ConfigureAwait(true);
        _isInitialized = true;
    }

    private void OnPlcConnectionStatusChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(IPlcConnectionStatusService.Current))
        {
            return;
        }

        Apply(_plcConnectionStatusService.Current);
    }

    private void Apply(PlcStartupConnectionResult result)
    {
        PlcConnectionStatusText = result.Message;
        PlcConnectionStatusBackground = result.Status switch
        {
            PlcStartupConnectionStatus.Connecting => Brushes.Goldenrod,
            PlcStartupConnectionStatus.Connected => Brushes.ForestGreen,
            PlcStartupConnectionStatus.NotConfigured => Brushes.DimGray,
            PlcStartupConnectionStatus.Uninitialized => Brushes.Gray,
            _ => Brushes.Firebrick
        };
    }

    private async Task RefreshAsync()
    {
        await RefreshAsync(CancellationToken.None).ConfigureAwait(true);
    }

    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        StatusMessage = LocalizedText.Get("ViewModels.ReplacePartVm.Loading");
        using var _ = _uiBusyService.Enter();

        try
        {
            var settings = await _appSettingsService.GetAsync(cancellationToken).ConfigureAwait(true);
            ResourceNumber = settings.ResourceNumber?.Trim() ?? string.Empty;
            Definitions.Clear();
            ReplacementHistory.Clear();
            _allDefinitions.Clear();

            if (string.IsNullOrWhiteSpace(ResourceNumber))
            {
                SelectedDefinition = null;
                StatusMessage = LocalizedText.Get("ViewModels.ReplacePartVm.ResourceNumberMissing");
                return;
            }

            var definitions = await _wearPartManagementService.GetDefinitionsByResourceNumberAsync(ResourceNumber, cancellationToken).ConfigureAwait(true);
            foreach (var definition in definitions.OrderBy(x => x.PartName, StringComparer.OrdinalIgnoreCase))
            {
                _allDefinitions.Add(definition);
                Definitions.Add(definition);
            }

            if (Definitions.Count == 0)
            {
                SelectedDefinition = null;
                StatusMessage = LocalizedText.Format("ViewModels.ReplacePartVm.DefinitionsEmpty", ResourceNumber);
                return;
            }

            SelectedDefinition ??= Definitions[0];
            StatusMessage = LocalizedText.Format("ViewModels.ReplacePartVm.DefinitionsLoaded", ResourceNumber, Definitions.Count);
            await LoadPreviewAndHistoryAsync(cancellationToken).ConfigureAwait(true);
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

    private async Task ReplaceAsync()
    {
        if (SelectedDefinition is null)
        {
            return;
        }

        IsBusy = true;
        StatusMessage = LocalizedText.Format("ViewModels.ReplacePartVm.Replacing", SelectedDefinition.PartName);
        using var _ = _uiBusyService.Enter();

        try
        {
            var record = await _wearPartReplacementService.ReplaceByScanAsync(new WearPartReplacementRequest
            {
                WearPartDefinitionId = SelectedDefinition.Id,
                NewBarcode = NewBarcode,
                ReplacementReason = SelectedReplacementReason,
                ReplacementMessage = ReplacementMessage
            }).ConfigureAwait(true);

            NewBarcode = string.Empty;
            ReplacementMessage = string.Empty;
            await LoadPreviewAndHistoryAsync(CancellationToken.None).ConfigureAwait(true);
            StatusMessage = LocalizedText.Format("ViewModels.ReplacePartVm.ReplaceSucceeded", record.PartName, record.NewBarcode);
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

    private bool CanRefresh()
    {
        return !IsBusy;
    }

    private bool CanReplace()
    {
        return !IsBusy
            && SelectedDefinition is not null
            && !string.IsNullOrWhiteSpace(NewBarcode)
            && !string.IsNullOrWhiteSpace(SelectedReplacementReason);
    }

    private void ApplySelectedDefinition(WearPartDefinition? definition)
    {
        InputMode = definition?.InputMode ?? string.Empty;
        CodeMinLengthText = definition?.CodeMinLength.ToString() ?? "0";
        CodeMaxLengthText = definition?.CodeMaxLength.ToString() ?? "0";

        if (definition is null)
        {
            CurrentValue = "0";
            WarningValue = "0";
            ShutdownValue = "0";
            LastBarcode = string.Empty;
        }
    }

    private async Task LoadPreviewAndHistoryAsync(CancellationToken cancellationToken)
    {
        var definition = SelectedDefinition;
        if (definition is null || IsBusy && cancellationToken == CancellationToken.None)
        {
            return;
        }

        try
        {
            var preview = await _wearPartReplacementService.GetReplacementPreviewAsync(definition.Id, cancellationToken).ConfigureAwait(true);
            CurrentValue = preview.CurrentValue;
            WarningValue = preview.WarningValue;
            ShutdownValue = preview.ShutdownValue;
            LastBarcode = preview.LastBarcode ?? string.Empty;

            var history = await _wearPartReplacementService.GetReplacementHistoryAsync(preview.ClientAppConfigurationId, cancellationToken).ConfigureAwait(true);
            ReplacementHistory.Clear();
            foreach (var record in history.Where(x => x.WearPartDefinitionId == definition.Id))
            {
                ReplacementHistory.Add(record);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }
}