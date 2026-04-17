using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WearPartsControl.ApplicationServices;
using WearPartsControl.ApplicationServices.PartServices;
using WearPartsControl.Exceptions;

namespace WearPartsControl.ViewModels;

public abstract class WearPartEditorViewModelBase : ObservableObject
{
    private readonly IWearPartManagementService _wearPartManagementService;
    private readonly IUiBusyService _uiBusyService;
    private Guid _id;
    private Guid _clientAppConfigurationId;
    private bool _isBusy;
    private string _resourceNumber = string.Empty;
    private string _partName = string.Empty;
    private string _inputMode = "Barcode";
    private string _currentValueAddress = string.Empty;
    private string _currentValueDataType = "INT32";
    private string _warningValueAddress = string.Empty;
    private string _warningValueDataType = "INT32";
    private string _shutdownValueAddress = string.Empty;
    private string _shutdownValueDataType = "INT32";
    private bool _isShutdown;
    private string _codeMinLength = "0";
    private string _codeMaxLength = "30";
    private string _lifetimeType = "Count";
    private string _plcZeroClearAddress = string.Empty;
    private string _barcodeWriteAddress = string.Empty;
    private string _statusMessage = string.Empty;

    protected WearPartEditorViewModelBase(
        IWearPartManagementService wearPartManagementService,
        IUiBusyService uiBusyService)
    {
        _wearPartManagementService = wearPartManagementService;
        _uiBusyService = uiBusyService;

        foreach (var item in new[] { "Barcode", "Manual", "Scanner" })
        {
            InputModes.Add(item);
        }

        foreach (var item in new[] { "Count", "Time", "Json" })
        {
            LifetimeTypes.Add(item);
        }

        foreach (var item in new[] { "INT32", "FLOAT", "DOUBLE", "BOOL", "STRING", "JSON" })
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
        set => SetProperty(ref _partName, value);
    }

    public string InputMode
    {
        get => _inputMode;
        set => SetProperty(ref _inputMode, value);
    }

    public string CurrentValueAddress
    {
        get => _currentValueAddress;
        set => SetProperty(ref _currentValueAddress, value);
    }

    public string CurrentValueDataType
    {
        get => _currentValueDataType;
        set => SetProperty(ref _currentValueDataType, value);
    }

    public string WarningValueAddress
    {
        get => _warningValueAddress;
        set => SetProperty(ref _warningValueAddress, value);
    }

    public string WarningValueDataType
    {
        get => _warningValueDataType;
        set => SetProperty(ref _warningValueDataType, value);
    }

    public string ShutdownValueAddress
    {
        get => _shutdownValueAddress;
        set => SetProperty(ref _shutdownValueAddress, value);
    }

    public string ShutdownValueDataType
    {
        get => _shutdownValueDataType;
        set => SetProperty(ref _shutdownValueDataType, value);
    }

    public bool IsShutdown
    {
        get => _isShutdown;
        set => SetProperty(ref _isShutdown, value);
    }

    public string CodeMinLength
    {
        get => _codeMinLength;
        set => SetProperty(ref _codeMinLength, value);
    }

    public string CodeMaxLength
    {
        get => _codeMaxLength;
        set => SetProperty(ref _codeMaxLength, value);
    }

    public string LifetimeType
    {
        get => _lifetimeType;
        set => SetProperty(ref _lifetimeType, value);
    }

    public string PlcZeroClearAddress
    {
        get => _plcZeroClearAddress;
        set => SetProperty(ref _plcZeroClearAddress, value);
    }

    public string BarcodeWriteAddress
    {
        get => _barcodeWriteAddress;
        set => SetProperty(ref _barcodeWriteAddress, value);
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
        InputMode = "Barcode";
        CurrentValueAddress = string.Empty;
        CurrentValueDataType = "INT32";
        WarningValueAddress = string.Empty;
        WarningValueDataType = "INT32";
        ShutdownValueAddress = string.Empty;
        ShutdownValueDataType = "INT32";
        IsShutdown = false;
        CodeMinLength = "0";
        CodeMaxLength = "30";
        LifetimeType = "Count";
        PlcZeroClearAddress = string.Empty;
        BarcodeWriteAddress = string.Empty;
        StatusMessage = string.IsNullOrWhiteSpace(ResourceNumber)
            ? "当前未配置资源号，无法保存易损件。"
            : $"当前资源号：{ResourceNumber}";
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
        LifetimeType = definition.LifetimeType;
        PlcZeroClearAddress = definition.PlcZeroClearAddress;
        BarcodeWriteAddress = definition.BarcodeWriteAddress;
        StatusMessage = $"正在编辑资源号 {ResourceNumber} 的易损件。";
    }

    protected abstract Task<WearPartDefinition> PersistAsync(WearPartDefinition definition, CancellationToken cancellationToken);

    protected IWearPartManagementService WearPartManagementService => _wearPartManagementService;

    private bool CanSave()
    {
        return !IsBusy && ClientAppConfigurationId != Guid.Empty && !string.IsNullOrWhiteSpace(ResourceNumber);
    }

    private async Task SaveAsync()
    {
        var enteredAt = DateTimeOffset.UtcNow;
        IsBusy = true;
        StatusMessage = "正在保存易损件...";
        using var _ = _uiBusyService.Enter();

        try
        {
            await EnsureLoadingRenderedAsync().ConfigureAwait(true);
            var definition = BuildDefinition();
            await PersistAsync(definition, CancellationToken.None).ConfigureAwait(true);
            await EnsureMinimumBusyDurationAsync(enteredAt).ConfigureAwait(true);
            StatusMessage = "保存成功。";
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
            throw new UserFriendlyException("条码最小长度必须是整数。");
        }

        if (!int.TryParse(CodeMaxLength?.Trim(), out var codeMaxLength))
        {
            throw new UserFriendlyException("条码最大长度必须是整数。");
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
            LifetimeType = LifetimeType,
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
}