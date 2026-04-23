using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Media;
using WearPartsControl.ApplicationServices;
using WearPartsControl.ApplicationServices.AppSettings;
using WearPartsControl.ApplicationServices.ClientAppInfo;
using WearPartsControl.ApplicationServices.Localization;
using WearPartsControl.ApplicationServices.PartServices;

namespace WearPartsControl.ViewModels;

public sealed class ReplacePartViewModel : ObservableObject
{
    private readonly IAppSettingsService _appSettingsService;
    private readonly IClientAppInfoService _clientAppInfoService;
    private readonly IWearPartManagementService _wearPartManagementService;
    private readonly IWearPartReplacementService _wearPartReplacementService;
    private readonly IToolChangeManagementService _toolChangeManagementService;
    private readonly IToolChangeSelectionService _toolChangeSelectionService;
    private readonly IUiDispatcher _uiDispatcher;
    private readonly IUiBusyService _uiBusyService;
    private readonly List<WearPartDefinition> _allDefinitions = new();
    private bool _isWearPartMonitoringEnabled = true;
    private string _wearPartMonitoringStatusText = LocalizedText.Get("ViewModels.ClientAppInfoVm.WearPartMonitoringEnabledStatus");
    private Brush _wearPartMonitoringStatusBackground = Brushes.ForestGreen;
    private WearPartDefinition? _selectedDefinition;
    private string _resourceNumber = string.Empty;
    private string _procedureCode = string.Empty;
    private string _inputMode = string.Empty;
    private int? _codeMinLength;
    private int? _codeMaxLength;
    private double? _currentValue;
    private double? _warningValue;
    private double? _shutdownValue;
    private string _lastBarcode = string.Empty;
    private string _newBarcode = string.Empty;
    private string _selectedReplacementReason = string.Empty;
    private string _selectedToolCode = string.Empty;
    private string _selectedAbSide = string.Empty;
    private string _replacementMessage = string.Empty;
    private string _statusMessage = LocalizedText.Get("ViewModels.ReplacePartVm.PromptSelectAndLoadPreview");
    private bool _isBusy;
    private bool _isInitialized;
    private bool _isApplyingToolCode;
    private int _selectionLoadVersion;

    public ReplacePartViewModel(
        IAppSettingsService appSettingsService,
        IClientAppInfoService clientAppInfoService,
        IWearPartManagementService wearPartManagementService,
        IWearPartReplacementService wearPartReplacementService,
        IToolChangeManagementService toolChangeManagementService,
        IToolChangeSelectionService toolChangeSelectionService,
        IUiDispatcher uiDispatcher,
        IUiBusyService uiBusyService)
    {
        _appSettingsService = appSettingsService;
        _clientAppInfoService = clientAppInfoService;
        _wearPartManagementService = wearPartManagementService;
        _wearPartReplacementService = wearPartReplacementService;
        _toolChangeManagementService = toolChangeManagementService;
        _toolChangeSelectionService = toolChangeSelectionService;
        _uiDispatcher = uiDispatcher;
        _uiBusyService = uiBusyService;
        _appSettingsService.SettingsSaved += OnAppSettingsSaved;
        RefreshCommand = new AsyncRelayCommand(() => RefreshAsync(), CanRefresh);
        ReplaceCommand = new AsyncRelayCommand(ReplaceAsync, CanReplace);

        foreach (var reason in WearPartReplacementReason.All)
        {
            ReplacementReasons.Add(reason);
        }

        foreach (var item in new[] { "A", "B" })
        {
            AbSideOptions.Add(item);
        }

        SelectedReplacementReason = ReplacementReasons.FirstOrDefault()?.Code ?? string.Empty;
    }

    public ObservableCollection<WearPartDefinition> Definitions { get; } = new();

    public ObservableCollection<WearPartReplacementReasonOption> ReplacementReasons { get; } = new();

    public ObservableCollection<ToolChangeDefinition> ToolCodeOptions { get; } = new();

    public ObservableCollection<string> AbSideOptions { get; } = new();

    public ObservableCollection<WearPartReplacementRecord> ReplacementHistory { get; } = new();

    public IAsyncRelayCommand RefreshCommand { get; }

    public IAsyncRelayCommand ReplaceCommand { get; }

    public bool IsReplaceEnabled => CanReplace();

    public bool IsWearPartMonitoringEnabled
    {
        get => _isWearPartMonitoringEnabled;
        private set
        {
            if (SetProperty(ref _isWearPartMonitoringEnabled, value))
            {
                OnPropertyChanged(nameof(IsTabContentEnabled));
                RefreshCommand.NotifyCanExecuteChanged();
                NotifyReplaceStateChanged();
            }
        }
    }

    public bool IsTabContentEnabled => IsWearPartMonitoringEnabled;

    public string WearPartMonitoringStatusText
    {
        get => _wearPartMonitoringStatusText;
        private set => SetProperty(ref _wearPartMonitoringStatusText, value);
    }

    public Brush WearPartMonitoringStatusBackground
    {
        get => _wearPartMonitoringStatusBackground;
        private set => SetProperty(ref _wearPartMonitoringStatusBackground, value);
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
                NotifyReplaceStateChanged();
                _ = LoadSelectedDefinitionDetailsAsync(value, CancellationToken.None, Interlocked.Increment(ref _selectionLoadVersion));
            }
        }
    }

    public string InputMode
    {
        get => _inputMode;
        private set => SetProperty(ref _inputMode, value);
    }

    public int? CodeMinLength
    {
        get => _codeMinLength;
        private set => SetProperty(ref _codeMinLength, value);
    }

    public int? CodeMaxLength
    {
        get => _codeMaxLength;
        private set => SetProperty(ref _codeMaxLength, value);
    }

    public double? CurrentValue
    {
        get => _currentValue;
        private set => SetProperty(ref _currentValue, value);
    }

    public double? WarningValue
    {
        get => _warningValue;
        private set => SetProperty(ref _warningValue, value);
    }

    public double? ShutdownValue
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
                NotifyReplaceStateChanged();
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
                NotifyReplaceStateChanged();
            }
        }
    }

    public string SelectedToolCode
    {
        get => _selectedToolCode;
        set
        {
            if (SetProperty(ref _selectedToolCode, value))
            {
                NotifyReplaceStateChanged();
                if (!_isApplyingToolCode && IsToolValidationEnabled && SelectedDefinition is not null)
                {
                    _ = SaveToolCodeSelectionAsync(SelectedDefinition.Id, value, CancellationToken.None);
                }
            }
        }
    }

    public bool IsToolValidationEnabled => ToolCodeReplacementGuard.RequiresToolCodeValidation(_procedureCode);

    public string SelectedAbSide
    {
        get => _selectedAbSide;
        set
        {
            if (SetProperty(ref _selectedAbSide, value))
            {
                NotifyReplaceStateChanged();
            }
        }
    }

    public bool IsCoatingValidationEnabled => CoatingSpacerReplacementGuard.RequiresCoatingValidation(_procedureCode);

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
                NotifyReplaceStateChanged();
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

    private void OnAppSettingsSaved(object? sender, AppSettings settings)
    {
        _uiDispatcher.Run(() =>
        {
            ApplyWearPartMonitoringStatus(settings.IsWearPartMonitoringEnabled);
            if (!settings.IsWearPartMonitoringEnabled)
            {
                StatusMessage = LocalizedText.Get("ViewModels.ReplacePartVm.MonitoringDisabledOperationBlocked");
            }
        });
    }

    private void ApplyWearPartMonitoringStatus(bool isEnabled)
    {
        IsWearPartMonitoringEnabled = isEnabled;
        WearPartMonitoringStatusText = isEnabled
            ? LocalizedText.Get("ViewModels.ClientAppInfoVm.WearPartMonitoringEnabledStatus")
            : LocalizedText.Get("ViewModels.ClientAppInfoVm.WearPartMonitoringDisabledStatus");
        WearPartMonitoringStatusBackground = isEnabled
            ? Brushes.ForestGreen
            : Brushes.DimGray;
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
        using var _ = _uiBusyService.Enter(LocalizedText.Get("ViewModels.ReplacePartVm.Loading"));

        try
        {
            var settings = await _appSettingsService.GetAsync(cancellationToken).ConfigureAwait(true);
            ResourceNumber = settings.ResourceNumber?.Trim() ?? string.Empty;
            ApplyWearPartMonitoringStatus(settings.IsWearPartMonitoringEnabled);
            var clientInfo = await _clientAppInfoService.GetAsync(cancellationToken).ConfigureAwait(true);
            _procedureCode = clientInfo.ProcedureCode?.Trim() ?? string.Empty;
            OnPropertyChanged(nameof(IsToolValidationEnabled));
            OnPropertyChanged(nameof(IsCoatingValidationEnabled));
            Definitions.Clear();
            ReplacementHistory.Clear();
            _allDefinitions.Clear();
            ToolCodeOptions.Clear();
            if (!IsCoatingValidationEnabled)
            {
                SelectedAbSide = string.Empty;
            }

            if (!IsWearPartMonitoringEnabled)
            {
                SelectedDefinition = null;
                SetSelectedToolCode(string.Empty);
                StatusMessage = LocalizedText.Get("ViewModels.ReplacePartVm.MonitoringDisabledOperationBlocked");
                return;
            }

            if (string.IsNullOrWhiteSpace(ResourceNumber))
            {
                SelectedDefinition = null;
                SetSelectedToolCode(string.Empty);
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
                SetSelectedToolCode(string.Empty);
                StatusMessage = LocalizedText.Format("ViewModels.ReplacePartVm.DefinitionsEmpty", ResourceNumber);
                return;
            }

            var selectedDefinition = SelectedDefinition is not null
                ? Definitions.FirstOrDefault(x => x.Id == SelectedDefinition.Id) ?? Definitions[0]
                : Definitions[0];

            if (!ReferenceEquals(SelectedDefinition, selectedDefinition))
            {
                SelectedDefinition = selectedDefinition;
            }
            else
            {
                ApplySelectedDefinition(selectedDefinition);
            }

            StatusMessage = IsWearPartMonitoringEnabled
                ? LocalizedText.Format("ViewModels.ReplacePartVm.DefinitionsLoaded", ResourceNumber, Definitions.Count)
                : LocalizedText.Get("ViewModels.ReplacePartVm.MonitoringDisabledOperationBlocked");
            await LoadSelectedDefinitionDetailsAsync(selectedDefinition, cancellationToken, Interlocked.Increment(ref _selectionLoadVersion)).ConfigureAwait(true);
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
        using var _ = _uiBusyService.Enter(LocalizedText.Format("ViewModels.ReplacePartVm.Replacing", SelectedDefinition.PartName));

        try
        {
            var record = await _wearPartReplacementService.ReplaceByScanAsync(new WearPartReplacementRequest
            {
                WearPartDefinitionId = SelectedDefinition.Id,
                NewBarcode = NewBarcode,
                ToolCode = SelectedToolCode,
                SelectedAbSide = SelectedAbSide,
                ReplacementReason = SelectedReplacementReason,
                ReplacementMessage = ReplacementMessage
            }).ConfigureAwait(true);

            ApplyReplacementRecord(record);
            NewBarcode = string.Empty;
            ReplacementMessage = string.Empty;
            await LoadSelectedDefinitionDetailsAsync(SelectedDefinition, CancellationToken.None, Interlocked.Increment(ref _selectionLoadVersion)).ConfigureAwait(true);
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
        return !IsBusy && IsWearPartMonitoringEnabled;
    }

    private bool CanReplace()
    {
        return !IsBusy
            && IsWearPartMonitoringEnabled
            && SelectedDefinition is not null
            && !string.IsNullOrWhiteSpace(NewBarcode)
            && (!IsToolValidationEnabled || !string.IsNullOrWhiteSpace(SelectedToolCode))
            && (!IsCoatingValidationEnabled || !string.IsNullOrWhiteSpace(SelectedAbSide))
            && !string.IsNullOrWhiteSpace(SelectedReplacementReason);
    }

    public ReplacePartConfirmationContext GetReplacementConfirmationContext()
    {
        var normalizedBarcode = NewBarcode?.Trim() ?? string.Empty;
        if (SelectedDefinition is null || string.IsNullOrWhiteSpace(normalizedBarcode))
        {
            return ReplacePartConfirmationContext.Empty;
        }

        var latestRemovalRecord = ReplacementHistory
            .Where(x => string.Equals(x.OldBarcode?.Trim(), normalizedBarcode, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.ReplacedAt)
            .FirstOrDefault();

        if (latestRemovalRecord is null)
        {
            return new ReplacePartConfirmationContext(
                SelectedDefinition.PartName,
                normalizedBarcode,
                IsReturningOldPart: false,
                HasReachedWarningLifetime: false,
                CurrentValueText: string.Empty,
                WarningValueText: string.Empty,
                ShutdownValueText: string.Empty);
        }

        var currentValue = TryParseRecordValue(latestRemovalRecord.CurrentValue, SelectedDefinition.CurrentValueDataType, SelectedDefinition.CurrentValueAddress);
        var warningValue = TryParseRecordValue(latestRemovalRecord.WarningValue, SelectedDefinition.WarningValueDataType, SelectedDefinition.WarningValueAddress);

        return new ReplacePartConfirmationContext(
            SelectedDefinition.PartName,
            normalizedBarcode,
            IsReturningOldPart: true,
            HasReachedWarningLifetime: currentValue.HasValue && warningValue.HasValue && currentValue.Value >= warningValue.Value,
            CurrentValueText: latestRemovalRecord.CurrentValue,
            WarningValueText: latestRemovalRecord.WarningValue,
            ShutdownValueText: latestRemovalRecord.ShutdownValue);
    }

    private void ApplySelectedDefinition(WearPartDefinition? definition)
    {
        InputMode = definition?.InputMode ?? string.Empty;
        CodeMinLength = definition?.CodeMinLength;
        CodeMaxLength = definition?.CodeMaxLength;

        if (definition is null)
        {
            CurrentValue = null;
            WarningValue = null;
            ShutdownValue = null;
            LastBarcode = LocalizedText.Get("ViewModels.ReplacePartVm.LastBarcodeEmpty");
            ToolCodeOptions.Clear();
            SetSelectedToolCode(string.Empty);
            NotifyReplaceStateChanged();
        }
    }

    private void NotifyReplaceStateChanged()
    {
        ReplaceCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(IsReplaceEnabled));
    }

    private async Task LoadSelectedDefinitionDetailsAsync(WearPartDefinition? definition, CancellationToken cancellationToken, int loadVersion)
    {
        if (definition is null)
        {
            return;
        }

        try
        {
            await LoadToolCodeStateAsync(definition, cancellationToken).ConfigureAwait(true);
            var preview = await _wearPartReplacementService.GetReplacementPreviewAsync(definition.Id, cancellationToken).ConfigureAwait(true);
            if (loadVersion != _selectionLoadVersion || SelectedDefinition?.Id != definition.Id)
            {
                return;
            }

            CurrentValue = ParsePreviewValue(preview.CurrentValue, definition.CurrentValueDataType, definition.CurrentValueAddress);
            WarningValue = ParsePreviewValue(preview.WarningValue, definition.WarningValueDataType, definition.WarningValueAddress);
            ShutdownValue = ParsePreviewValue(preview.ShutdownValue, definition.ShutdownValueDataType, definition.ShutdownValueAddress);

            var history = await _wearPartReplacementService.GetReplacementHistoryAsync(preview.ClientAppConfigurationId, cancellationToken).ConfigureAwait(true);
            if (loadVersion != _selectionLoadVersion || SelectedDefinition?.Id != definition.Id)
            {
                return;
            }

            var filteredHistory = history.Where(x => x.WearPartDefinitionId == definition.Id).ToArray();
            ReplacementHistory.Clear();
            foreach (var record in filteredHistory)
            {
                ReplacementHistory.Add(record);
            }

            LastBarcode = string.IsNullOrWhiteSpace(preview.LastBarcode)
                ? LocalizedText.Get("ViewModels.ReplacePartVm.LastBarcodeEmpty")
                : preview.LastBarcode.Trim();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    private async Task LoadToolCodeStateAsync(WearPartDefinition definition, CancellationToken cancellationToken)
    {
        if (!IsToolValidationEnabled)
        {
            ToolCodeOptions.Clear();
            SetSelectedToolCode(string.Empty);
            return;
        }

        var toolChanges = await _toolChangeManagementService.GetAllAsync(cancellationToken).ConfigureAwait(true);
        var state = await _toolChangeSelectionService.GetStateAsync(definition.Id, cancellationToken).ConfigureAwait(true);
        ToolCodeOptions.Clear();
        foreach (var toolChange in toolChanges)
        {
            ToolCodeOptions.Add(toolChange);
        }

        var associatedCode = definition.ToolChangeId.HasValue
            ? toolChanges.FirstOrDefault(x => x.Id == definition.ToolChangeId.Value)?.Code
            : null;
        var selectedCode = !string.IsNullOrWhiteSpace(associatedCode)
            ? associatedCode
            : toolChanges.Any(x => string.Equals(x.Code, state.SelectedToolCode, StringComparison.OrdinalIgnoreCase))
                ? state.SelectedToolCode
                : toolChanges.FirstOrDefault()?.Code ?? string.Empty;
        SetSelectedToolCode(selectedCode);
    }

    private async Task SaveToolCodeSelectionAsync(Guid wearPartDefinitionId, string toolCode, CancellationToken cancellationToken)
    {
        try
        {
            await _toolChangeSelectionService.SaveSelectionAsync(wearPartDefinitionId, toolCode, cancellationToken).ConfigureAwait(true);
        }
        catch
        {
            // 持久化工具编码失败不应中断更换页的主交互。
        }
    }

    private void SetSelectedToolCode(string value)
    {
        _isApplyingToolCode = true;
        try
        {
            SelectedToolCode = value;
        }
        finally
        {
            _isApplyingToolCode = false;
        }
    }

    private void ApplyReplacementRecord(WearPartReplacementRecord record)
    {
        if (SelectedDefinition is null || record.WearPartDefinitionId != SelectedDefinition.Id)
        {
            return;
        }

        var existingRecord = ReplacementHistory.FirstOrDefault(x => x.Id == record.Id);
        if (existingRecord is not null)
        {
            var existingIndex = ReplacementHistory.IndexOf(existingRecord);
            if (existingIndex >= 0)
            {
                ReplacementHistory[existingIndex] = record;
            }
        }
        else
        {
            ReplacementHistory.Insert(0, record);
        }

        LastBarcode = string.IsNullOrWhiteSpace(record.NewBarcode)
            ? LocalizedText.Get("ViewModels.ReplacePartVm.LastBarcodeEmpty")
            : record.NewBarcode.Trim();
    }

    private static double? ParsePreviewValue(string rawValue, string dataType, string address)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return null;
        }

        return WearPartReplacementValueParser.ParseDouble(rawValue, dataType, address);
    }

    private static double? TryParseRecordValue(string rawValue, string dataType, string address)
    {
        try
        {
            return ParsePreviewValue(rawValue, dataType, address);
        }
        catch
        {
            return null;
        }
    }

    public sealed record ReplacePartConfirmationContext(
        string PartName,
        string Barcode,
        bool IsReturningOldPart,
        bool HasReachedWarningLifetime,
        string CurrentValueText,
        string WarningValueText,
        string ShutdownValueText)
    {
        public static ReplacePartConfirmationContext Empty { get; } = new(string.Empty, string.Empty, false, false, string.Empty, string.Empty, string.Empty);
    }
}