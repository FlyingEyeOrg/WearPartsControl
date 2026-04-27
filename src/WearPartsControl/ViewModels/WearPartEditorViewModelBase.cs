using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WearPartsControl.ApplicationServices;
using WearPartsControl.ApplicationServices.Localization;
using WearPartsControl.ApplicationServices.PartServices;
using WearPartsControl.Exceptions;

namespace WearPartsControl.ViewModels;

public abstract class WearPartEditorViewModelBase : LocalizedViewModelBase
{
    private static readonly IReadOnlyDictionary<string, string> LifetimeTypeAliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["Meter"] = "Meter",
        ["Count"] = "Count",
        ["Time"] = "Time",
        ["记米"] = "Meter",
        ["计次"] = "Count",
        ["计时"] = "Time"
    };

    private const string DefaultCreateInputMode = "Manual";
    private const string DefaultCreateDataType = "FLOAT";
    private const string InputModeManualCode = "Manual";
    private const string InputModeScannerCode = "Scanner";
    private const string LifetimeTypeMeterCode = "Meter";
    private const string LifetimeTypeCountCode = "Count";
    private const string LifetimeTypeTimeCode = "Time";
    private const string DefaultCreateLifetimeType = LifetimeTypeTimeCode;
    private const string DefaultCreateCodeMinLength = "1";
    private const string DefaultCreateCodeMaxLength = "128";

    private readonly IWearPartManagementService _wearPartManagementService;
    private readonly IWearPartTypeService _wearPartTypeService;
    private readonly IUiBusyService _uiBusyService;
    private Guid _id;
    private Guid _clientAppConfigurationId;
    private bool _isBusy;
    private string _resourceNumber = string.Empty;
    private string _partName = string.Empty;
    private string _inputMode = DefaultCreateInputMode;
    private string _currentValueAddress = string.Empty;
    private string _currentValueDataType = DefaultCreateDataType;
    private string _warningValueAddress = string.Empty;
    private string _warningValueDataType = DefaultCreateDataType;
    private string _shutdownValueAddress = string.Empty;
    private string _shutdownValueDataType = DefaultCreateDataType;
    private bool _isShutdown;
    private string _codeMinLength = DefaultCreateCodeMinLength;
    private string _codeMaxLength = DefaultCreateCodeMaxLength;
    private string _lifetimeType = DefaultCreateLifetimeType;
    private Guid? _selectedWearPartTypeId;
    private string _plcZeroClearAddress = string.Empty;
    private string _barcodeWriteAddress = string.Empty;
    private string _statusMessage = string.Empty;
    private Func<string>? _statusMessageFactory;

    protected WearPartEditorViewModelBase(
        IWearPartManagementService wearPartManagementService,
        IWearPartTypeService wearPartTypeService,
        IUiBusyService uiBusyService)
    {
        _wearPartManagementService = wearPartManagementService;
        _wearPartTypeService = wearPartTypeService;
        _uiBusyService = uiBusyService;

        RefreshInputModeOptions();

        RefreshLifetimeTypeOptions();

        foreach (var item in new[] { "FLOAT", "DOUBLE", "INT32", "BOOL", "STRING", "JSON" })
        {
            DataTypes.Add(item);
        }

        SaveCommand = new AsyncRelayCommand(SaveAsync, CanSave);
        CancelCommand = new RelayCommand(Cancel);
    }

    public event EventHandler<bool?>? RequestClose;

    public ObservableCollection<InputModeOption> InputModes { get; } = new();

    public ObservableCollection<LifetimeTypeOption> LifetimeTypes { get; } = new();
    public ObservableCollection<WearPartTypeDefinition> WearPartTypes { get; } = new();

    public ObservableCollection<string> DataTypes { get; } = new();

    public Guid? SelectedWearPartTypeId
    {
        get => _selectedWearPartTypeId;
        set => SetEditorProperty(ref _selectedWearPartTypeId, value);
    }

    public IAsyncRelayCommand SaveCommand { get; }

    public IRelayCommand CancelCommand { get; }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                SaveCommand.NotifyCanExecuteChanged();
                OnPropertyChanged(nameof(IsNotBusy));
            }
        }
    }

    public bool IsNotBusy => !IsBusy;

    public Guid ClientAppConfigurationId
    {
        get => _clientAppConfigurationId;
        protected set
        {
            if (SetProperty(ref _clientAppConfigurationId, value))
            {
                SaveCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string ResourceNumber
    {
        get => _resourceNumber;
        protected set => SetProperty(ref _resourceNumber, value);
    }

    public string PartName
    {
        get => _partName;
        set => SetEditorProperty(ref _partName, value);
    }

    public string InputMode
    {
        get => _inputMode;
        set => SetEditorProperty(ref _inputMode, value?.Trim() ?? string.Empty);
    }

    public string CurrentValueAddress
    {
        get => _currentValueAddress;
        set => SetEditorProperty(ref _currentValueAddress, value);
    }

    public string CurrentValueDataType
    {
        get => _currentValueDataType;
        set => SetEditorProperty(ref _currentValueDataType, value);
    }

    public string WarningValueAddress
    {
        get => _warningValueAddress;
        set => SetEditorProperty(ref _warningValueAddress, value);
    }

    public string WarningValueDataType
    {
        get => _warningValueDataType;
        set => SetEditorProperty(ref _warningValueDataType, value);
    }

    public string ShutdownValueAddress
    {
        get => _shutdownValueAddress;
        set => SetEditorProperty(ref _shutdownValueAddress, value);
    }

    public string ShutdownValueDataType
    {
        get => _shutdownValueDataType;
        set => SetEditorProperty(ref _shutdownValueDataType, value);
    }

    public bool IsShutdown
    {
        get => _isShutdown;
        set => SetEditorProperty(ref _isShutdown, value);
    }

    public string CodeMinLength
    {
        get => _codeMinLength;
        set => SetEditorProperty(ref _codeMinLength, value);
    }

    public string CodeMaxLength
    {
        get => _codeMaxLength;
        set => SetEditorProperty(ref _codeMaxLength, value);
    }

    public string LifetimeType
    {
        get => _lifetimeType;
        set => SetEditorProperty(ref _lifetimeType, NormalizeLifetimeType(value));
    }

    public string PlcZeroClearAddress
    {
        get => _plcZeroClearAddress;
        set => SetEditorProperty(ref _plcZeroClearAddress, value);
    }

    public string BarcodeWriteAddress
    {
        get => _barcodeWriteAddress;
        set => SetEditorProperty(ref _barcodeWriteAddress, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        protected set => SetProperty(ref _statusMessage, value);
    }

    public async Task InitializeForCreateAsync(Guid clientAppConfigurationId, string resourceNumber, CancellationToken cancellationToken = default)
    {
        await LoadWearPartTypesAsync(null, cancellationToken).ConfigureAwait(true);
        _id = Guid.Empty;
        ClientAppConfigurationId = clientAppConfigurationId;
        ResourceNumber = resourceNumber?.Trim() ?? string.Empty;
        PartName = string.Empty;
        InputMode = DefaultCreateInputMode;
        CurrentValueAddress = string.Empty;
        CurrentValueDataType = DefaultCreateDataType;
        WarningValueAddress = string.Empty;
        WarningValueDataType = DefaultCreateDataType;
        ShutdownValueAddress = string.Empty;
        ShutdownValueDataType = DefaultCreateDataType;
        IsShutdown = false;
        CodeMinLength = DefaultCreateCodeMinLength;
        CodeMaxLength = DefaultCreateCodeMaxLength;
        LifetimeType = DefaultCreateLifetimeType;
        SelectedWearPartTypeId = ResolveDefaultWearPartTypeId();
        PlcZeroClearAddress = string.Empty;
        BarcodeWriteAddress = string.Empty;
        SetLocalizedStatusMessage(() => string.IsNullOrWhiteSpace(ResourceNumber)
            ? LocalizedText.Get("ViewModels.WearPartEditorVm.ResourceNumberMissing")
            : LocalizedText.Format("ViewModels.WearPartEditorVm.CurrentResourceNumber", ResourceNumber));
        SaveCommand.NotifyCanExecuteChanged();
    }

    public async Task InitializeForEditAsync(WearPartDefinition definition, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);

        await LoadWearPartTypesAsync(definition.WearPartTypeId, cancellationToken).ConfigureAwait(true);

        _id = definition.Id;
        ClientAppConfigurationId = definition.ClientAppConfigurationId;
        ResourceNumber = definition.ResourceNumber;
        PartName = definition.PartName;
        InputMode = definition.InputMode;
        CurrentValueAddress = definition.CurrentValueAddress;
        CurrentValueDataType = definition.CurrentValueDataType;
        WarningValueAddress = definition.WarningValueAddress;
        WarningValueDataType = definition.WarningValueDataType;
        ShutdownValueAddress = definition.ShutdownValueAddress;
        ShutdownValueDataType = definition.ShutdownValueDataType;
        IsShutdown = definition.IsShutdown;
        CodeMinLength = definition.CodeMinLength.ToString();
        CodeMaxLength = definition.CodeMaxLength.ToString();
        LifetimeType = NormalizeLifetimeType(definition.LifetimeType);
        SelectedWearPartTypeId = definition.WearPartTypeId ?? ResolveDefaultWearPartTypeId();
        PlcZeroClearAddress = definition.PlcZeroClearAddress;
        BarcodeWriteAddress = definition.BarcodeWriteAddress;
        SetLocalizedStatusMessage(() => LocalizedText.Format("ViewModels.WearPartEditorVm.Editing", ResourceNumber));
        SaveCommand.NotifyCanExecuteChanged();
    }

    protected abstract Task<WearPartDefinition> PersistAsync(WearPartDefinition definition, CancellationToken cancellationToken);

    protected IWearPartManagementService WearPartManagementService => _wearPartManagementService;

    private bool CanSave()
    {
        return !IsBusy
            && ClientAppConfigurationId != Guid.Empty
            && !string.IsNullOrWhiteSpace(ResourceNumber)
            && !string.IsNullOrWhiteSpace(PartName)
            && IsSupportedInputMode(InputMode)
            && !string.IsNullOrWhiteSpace(CurrentValueAddress)
            && !string.IsNullOrWhiteSpace(CurrentValueDataType)
            && !string.IsNullOrWhiteSpace(WarningValueAddress)
            && !string.IsNullOrWhiteSpace(WarningValueDataType)
            && !string.IsNullOrWhiteSpace(ShutdownValueAddress)
            && !string.IsNullOrWhiteSpace(ShutdownValueDataType)
            && !string.IsNullOrWhiteSpace(LifetimeType)
            && SelectedWearPartTypeId.HasValue
            && int.TryParse(CodeMinLength?.Trim(), out _)
            && int.TryParse(CodeMaxLength?.Trim(), out _);
    }

    private async Task SaveAsync()
    {
        var enteredAt = DateTimeOffset.UtcNow;
        IsBusy = true;
        SetLocalizedStatusMessage(() => LocalizedText.Get("ViewModels.WearPartEditorVm.Saving"));
        using var _ = _uiBusyService.Enter(LocalizedText.Get("ViewModels.WearPartEditorVm.Saving"));

        try
        {
            await EnsureLoadingRenderedAsync().ConfigureAwait(true);
            var definition = BuildDefinition();
            await PersistAsync(definition, CancellationToken.None).ConfigureAwait(true);
            await EnsureMinimumBusyDurationAsync(enteredAt).ConfigureAwait(true);
            SetLocalizedStatusMessage(() => LocalizedText.Get("ViewModels.WearPartEditorVm.Saved"));
            RequestClose?.Invoke(this, true);
        }
        catch (Exception ex)
        {
            await EnsureMinimumBusyDurationAsync(enteredAt).ConfigureAwait(true);
            SetRawStatusMessage(ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private WearPartDefinition BuildDefinition()
    {
        if (!int.TryParse(CodeMinLength?.Trim(), out var codeMinLength))
        {
            throw new UserFriendlyException(LocalizedText.Get("ViewModels.WearPartEditorVm.CodeMinInvalid"));
        }

        if (!int.TryParse(CodeMaxLength?.Trim(), out var codeMaxLength))
        {
            throw new UserFriendlyException(LocalizedText.Get("ViewModels.WearPartEditorVm.CodeMaxInvalid"));
        }

        return new WearPartDefinition
        {
            Id = _id,
            ClientAppConfigurationId = ClientAppConfigurationId,
            ResourceNumber = ResourceNumber,
            PartName = PartName,
            InputMode = InputMode.Trim(),
            CurrentValueAddress = CurrentValueAddress,
            CurrentValueDataType = CurrentValueDataType,
            WarningValueAddress = WarningValueAddress,
            WarningValueDataType = WarningValueDataType,
            ShutdownValueAddress = ShutdownValueAddress,
            ShutdownValueDataType = ShutdownValueDataType,
            IsShutdown = IsShutdown,
            CodeMinLength = codeMinLength,
            CodeMaxLength = codeMaxLength,
            LifetimeType = NormalizeLifetimeType(LifetimeType),
            WearPartTypeId = SelectedWearPartTypeId,
            ToolChangeId = null,
            PlcZeroClearAddress = PlcZeroClearAddress,
            BarcodeWriteAddress = BarcodeWriteAddress
        };
    }

    private async Task LoadWearPartTypesAsync(Guid? selectedWearPartTypeId, CancellationToken cancellationToken)
    {
        var wearPartTypes = await _wearPartTypeService.GetAllAsync(cancellationToken).ConfigureAwait(true);
        WearPartTypes.Clear();
        foreach (var wearPartType in wearPartTypes)
        {
            WearPartTypes.Add(wearPartType);
        }

        SelectedWearPartTypeId = selectedWearPartTypeId.HasValue && WearPartTypes.Any(x => x.Id == selectedWearPartTypeId.Value)
            ? selectedWearPartTypeId
            : ResolveDefaultWearPartTypeId();
    }

    private Guid? ResolveDefaultWearPartTypeId()
    {
        return WearPartTypes.FirstOrDefault(x => string.Equals(x.Code, WearPartTypeCodes.Uncategorized, StringComparison.OrdinalIgnoreCase))?.Id
            ?? WearPartTypes.FirstOrDefault()?.Id;
    }

    private void Cancel()
    {
        RequestClose?.Invoke(this, false);
    }

    private async Task EnsureMinimumBusyDurationAsync(DateTimeOffset busyEnteredAt, CancellationToken cancellationToken = default)
    {
        var elapsed = DateTimeOffset.UtcNow - busyEnteredAt;
        var remaining = _uiBusyService.MinimumBusyDuration - elapsed;
        if (remaining <= TimeSpan.Zero)
        {
            return;
        }

        await Task.Delay(remaining, cancellationToken).ConfigureAwait(true);
    }

    private static async Task EnsureLoadingRenderedAsync()
    {
        if (Application.Current?.Dispatcher is { } dispatcher)
        {
            await dispatcher.InvokeAsync(static () => { }, DispatcherPriority.Render);
            return;
        }

        await Task.Yield();
    }

    private void SetEditorProperty<T>(ref T field, T value)
    {
        if (SetProperty(ref field, value))
        {
            SaveCommand.NotifyCanExecuteChanged();
        }
    }

    private static string NormalizeLifetimeType(string? value)
    {
        var normalized = value?.Trim() ?? string.Empty;
        return LifetimeTypeAliases.TryGetValue(normalized, out var alias)
            ? alias
            : normalized;
    }

    private static bool IsSupportedInputMode(string? value)
    {
        var normalized = value?.Trim() ?? string.Empty;
        return string.Equals(normalized, InputModeManualCode, StringComparison.Ordinal)
            || string.Equals(normalized, InputModeScannerCode, StringComparison.Ordinal);
    }

    protected override void OnLocalizationRefreshed()
    {
        RefreshInputModeOptions();
        RefreshLifetimeTypeOptions();

        if (_statusMessageFactory is not null)
        {
            StatusMessage = _statusMessageFactory();
        }
    }

    private void RefreshInputModeOptions()
    {
        UpdateInputModeOptionDisplayName(InputModeManualCode, LocalizedText.Get("ViewModels.WearPartEditorVm.InputModeManual"));
        UpdateInputModeOptionDisplayName(InputModeScannerCode, LocalizedText.Get("ViewModels.WearPartEditorVm.InputModeScanner"));
    }

    private void UpdateInputModeOptionDisplayName(string code, string displayName)
    {
        var option = InputModes.FirstOrDefault(existing => string.Equals(existing.Code, code, StringComparison.Ordinal));

        if (option is null)
        {
            InputModes.Add(new InputModeOption(code, displayName));
            return;
        }

        option.DisplayName = displayName;
    }

    private void RefreshLifetimeTypeOptions()
    {
        UpdateLifetimeTypeOptionDisplayName(LifetimeTypeMeterCode, LocalizedText.Get("ViewModels.WearPartEditorVm.LifetimeTypeMeter"));
        UpdateLifetimeTypeOptionDisplayName(LifetimeTypeCountCode, LocalizedText.Get("ViewModels.WearPartEditorVm.LifetimeTypeCount"));
        UpdateLifetimeTypeOptionDisplayName(LifetimeTypeTimeCode, LocalizedText.Get("ViewModels.WearPartEditorVm.LifetimeTypeTime"));
    }

    private void UpdateLifetimeTypeOptionDisplayName(string code, string displayName)
    {
        var option = LifetimeTypes.FirstOrDefault(existing => string.Equals(existing.Code, code, StringComparison.Ordinal));

        if (option is null)
        {
            LifetimeTypes.Add(new LifetimeTypeOption(code, displayName));
            return;
        }

        option.DisplayName = displayName;
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