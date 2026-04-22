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

public abstract class WearPartEditorViewModelBase : ObservableObject
{
    private static readonly IReadOnlyDictionary<string, string> LifetimeTypeAliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["Meter"] = "记米",
        ["Count"] = "计次",
        ["Time"] = "计时",
        ["记米"] = "记米",
        ["计次"] = "计次",
        ["计时"] = "计时"
    };

    private const string DefaultCreateInputMode = "Manual";
    private const string DefaultCreateDataType = "FLOAT";
    private const string DefaultCreateLifetimeType = "计次";
    private const string DefaultCreateCodeMinLength = "0";
    private const string DefaultCreateCodeMaxLength = "0";

    private readonly IWearPartManagementService _wearPartManagementService;
    private readonly IToolChangeManagementService _toolChangeManagementService;
    private readonly IUiBusyService _uiBusyService;
    private Guid _id;
    private Guid _clientAppConfigurationId;
    private bool _isBusy;
    private bool _toolChangesLoaded;
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
    private Guid? _selectedToolChangeId;
    private string _plcZeroClearAddress = string.Empty;
    private string _barcodeWriteAddress = string.Empty;
    private string _statusMessage = string.Empty;

    protected WearPartEditorViewModelBase(
        IWearPartManagementService wearPartManagementService,
        IToolChangeManagementService toolChangeManagementService,
        IUiBusyService uiBusyService)
    {
        _wearPartManagementService = wearPartManagementService;
        _toolChangeManagementService = toolChangeManagementService;
        _uiBusyService = uiBusyService;

        foreach (var item in new[] { "Manual", "Scanner", "Barcode" })
        {
            InputModes.Add(item);
        }

        foreach (var item in new[] { "记米", "计次", "计时" })
        {
            LifetimeTypes.Add(item);
        }

        foreach (var item in new[] { "FLOAT", "DOUBLE", "INT32", "BOOL", "STRING", "JSON" })
        {
            DataTypes.Add(item);
        }

        SaveCommand = new AsyncRelayCommand(SaveAsync, CanSave);
        CancelCommand = new RelayCommand(Cancel);
    }

    public event EventHandler<bool?>? RequestClose;

    public ObservableCollection<string> InputModes { get; } = new();

    public ObservableCollection<string> LifetimeTypes { get; } = new();

    public ObservableCollection<string> DataTypes { get; } = new();

    public ObservableCollection<ToolChangeDefinition> ToolChangeOptions { get; } = new();

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
        set => SetEditorProperty(ref _inputMode, value);
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
        set => SetEditorProperty(ref _lifetimeType, value);
    }

    public string PlcZeroClearAddress
    {
        get => _plcZeroClearAddress;
        set => SetEditorProperty(ref _plcZeroClearAddress, value);
    }

    public Guid? SelectedToolChangeId
    {
        get => _selectedToolChangeId;
        set => SetEditorProperty(ref _selectedToolChangeId, value);
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

    public void InitializeForCreate(Guid clientAppConfigurationId, string resourceNumber)
    {
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
        SelectedToolChangeId = null;
        PlcZeroClearAddress = string.Empty;
        BarcodeWriteAddress = string.Empty;
        StatusMessage = string.IsNullOrWhiteSpace(ResourceNumber)
            ? LocalizedText.Get("ViewModels.WearPartEditorVm.ResourceNumberMissing")
            : LocalizedText.Format("ViewModels.WearPartEditorVm.CurrentResourceNumber", ResourceNumber);
        SaveCommand.NotifyCanExecuteChanged();
    }

    public void InitializeForEdit(WearPartDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

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
        SelectedToolChangeId = definition.ToolChangeId;
        PlcZeroClearAddress = definition.PlcZeroClearAddress;
        BarcodeWriteAddress = definition.BarcodeWriteAddress;
        StatusMessage = LocalizedText.Format("ViewModels.WearPartEditorVm.Editing", ResourceNumber);
        SaveCommand.NotifyCanExecuteChanged();
    }

    public async Task EnsureToolChangeOptionsLoadedAsync(CancellationToken cancellationToken = default)
    {
        if (_toolChangesLoaded)
        {
            return;
        }

        var toolChanges = await _toolChangeManagementService.GetAllAsync(cancellationToken).ConfigureAwait(true);
        ToolChangeOptions.Clear();
        foreach (var toolChange in toolChanges)
        {
            ToolChangeOptions.Add(toolChange);
        }

        if (SelectedToolChangeId.HasValue && ToolChangeOptions.All(x => x.Id != SelectedToolChangeId.Value))
        {
            SelectedToolChangeId = null;
        }

        _toolChangesLoaded = true;
    }

    protected abstract Task<WearPartDefinition> PersistAsync(WearPartDefinition definition, CancellationToken cancellationToken);

    protected IWearPartManagementService WearPartManagementService => _wearPartManagementService;

    private bool CanSave()
    {
        return !IsBusy
            && ClientAppConfigurationId != Guid.Empty
            && !string.IsNullOrWhiteSpace(ResourceNumber)
            && !string.IsNullOrWhiteSpace(PartName)
            && !string.IsNullOrWhiteSpace(InputMode)
            && !string.IsNullOrWhiteSpace(CurrentValueAddress)
            && !string.IsNullOrWhiteSpace(CurrentValueDataType)
            && !string.IsNullOrWhiteSpace(WarningValueAddress)
            && !string.IsNullOrWhiteSpace(WarningValueDataType)
            && !string.IsNullOrWhiteSpace(ShutdownValueAddress)
            && !string.IsNullOrWhiteSpace(ShutdownValueDataType)
            && !string.IsNullOrWhiteSpace(LifetimeType)
            && int.TryParse(CodeMinLength?.Trim(), out _)
            && int.TryParse(CodeMaxLength?.Trim(), out _);
    }

    private async Task SaveAsync()
    {
        var enteredAt = DateTimeOffset.UtcNow;
        IsBusy = true;
        StatusMessage = LocalizedText.Get("ViewModels.WearPartEditorVm.Saving");
        using var _ = _uiBusyService.Enter(LocalizedText.Get("ViewModels.WearPartEditorVm.Saving"));

        try
        {
            await EnsureLoadingRenderedAsync().ConfigureAwait(true);
            var definition = BuildDefinition();
            await PersistAsync(definition, CancellationToken.None).ConfigureAwait(true);
            await EnsureMinimumBusyDurationAsync(enteredAt).ConfigureAwait(true);
            StatusMessage = LocalizedText.Get("ViewModels.WearPartEditorVm.Saved");
            RequestClose?.Invoke(this, true);
        }
        catch (Exception ex)
        {
            await EnsureMinimumBusyDurationAsync(enteredAt).ConfigureAwait(true);
            StatusMessage = ex.Message;
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
            InputMode = InputMode,
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
            ToolChangeId = SelectedToolChangeId,
            PlcZeroClearAddress = PlcZeroClearAddress,
            BarcodeWriteAddress = BarcodeWriteAddress
        };
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
}