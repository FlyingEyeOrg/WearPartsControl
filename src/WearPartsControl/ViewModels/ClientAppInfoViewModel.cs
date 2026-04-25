using System.Collections.ObjectModel;
using System.IO;
using System.Globalization;
using System.Text.Json;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WearPartsControl.ApplicationServices;
using WearPartsControl.ApplicationServices.ClientAppInfo;
using WearPartsControl.ApplicationServices.Localization;
using WearPartsControl.ApplicationServices.PartServices;
using WearPartsControl.ApplicationServices.PlcService;
using WearPartsControl.ApplicationServices.LegacyImport;
using WearPartsControl.Exceptions;

namespace WearPartsControl.ViewModels;

public sealed class ClientAppInfoViewModel : LocalizedViewModelBase
{
    private readonly IClientAppInfoService _clientAppInfoService;
    private readonly IClientAppInfoSelectionOptionsProvider _selectionOptionsProvider;
    private readonly ILegacyConfigurationImportService _legacyConfigurationImportService;
    private readonly IPlcConnectionTestService _plcConnectionTestService;
    private readonly IPlcConnectionStatusService _plcConnectionStatusService;
    private readonly IWearPartMonitoringControlService _wearPartMonitoringControlService;
    private readonly IUiDispatcher _uiDispatcher;
    private readonly IUiBusyService _uiBusyService;
    private readonly List<SiteFactoryOption> _siteFactoryOptions = new();
    private Guid? _clientAppConfigurationId;
    private ClientAppInfoSnapshot _originalSnapshot = ClientAppInfoSnapshot.Empty;
    private bool _isBusy;
    private bool _isDirty;
    private bool _isInitialized;
    private bool _isUpdatingState;
    private string _siteCode = string.Empty;
    private string _factoryCode = string.Empty;
    private string _areaCode = string.Empty;
    private string _procedureCode = string.Empty;
    private string _equipmentCode = string.Empty;
    private string _resourceNumber = string.Empty;
    private string _plcProtocolType = string.Empty;
    private string _plcIpAddress = string.Empty;
    private string _plcPort = "102";
    private string _shutdownPointAddress = string.Empty;
    private bool _enableCutterMesValidation;
    private string _cutterMesWsdl = string.Empty;
    private string _cutterMesUser = string.Empty;
    private string _cutterMesPassword = string.Empty;
    private string _cutterMesSite = string.Empty;
    private string _siemensRack = "0";
    private string _siemensSlot = "0";
    private string _statusMessage = LocalizedText.Get("ViewModels.ClientAppInfoVm.PromptComplete");
    private bool _isStringReverse = true;
    private bool _isWearPartMonitoringEnabled;
    private Func<string>? _statusMessageFactory;

    public ClientAppInfoViewModel(
        IClientAppInfoService clientAppInfoService,
        IClientAppInfoSelectionOptionsProvider selectionOptionsProvider,
        ILegacyConfigurationImportService legacyConfigurationImportService,
        IPlcConnectionTestService plcConnectionTestService,
        IPlcConnectionStatusService plcConnectionStatusService,
        IWearPartMonitoringControlService wearPartMonitoringControlService,
        IUiDispatcher uiDispatcher,
        IUiBusyService uiBusyService)
    {
        _clientAppInfoService = clientAppInfoService;
        _selectionOptionsProvider = selectionOptionsProvider;
        _legacyConfigurationImportService = legacyConfigurationImportService;
        _plcConnectionTestService = plcConnectionTestService;
        _plcConnectionStatusService = plcConnectionStatusService;
        _wearPartMonitoringControlService = wearPartMonitoringControlService;
        _uiDispatcher = uiDispatcher;
        _uiBusyService = uiBusyService;
        SaveCommand = new AsyncRelayCommand(SaveAsync, CanSaveCommand);
        ImportLegacyConfigurationCommand = new RelayCommand(RequestImportLegacyConfiguration, CanImportLegacyConfigurationCommand);
        TestPlcConnectionCommand = new AsyncRelayCommand(TestPlcConnectionAsync, CanTestPlcConnectionCommand);
        ToggleWearPartMonitoringCommand = new AsyncRelayCommand(ToggleWearPartMonitoringAsync, CanToggleWearPartMonitoringCommand);
        _plcConnectionStatusService.PropertyChanged += OnPlcConnectionStatusChanged;

        foreach (var plcProtocolType in Enum.GetNames<PlcProtocolType>())
        {
            PlcProtocolTypes.Add(plcProtocolType);
        }

        SetLocalizedStatusMessage(() => LocalizedText.Get("ViewModels.ClientAppInfoVm.PromptComplete"));
    }

    public ObservableCollection<SiteOption> SiteOptions { get; } = new();

    public ObservableCollection<string> FactoryOptions { get; } = new();

    public ObservableCollection<string> AreaOptions { get; } = new();

    public ObservableCollection<string> ProcedureOptions { get; } = new();

    public ObservableCollection<string> PlcProtocolTypes { get; } = new();

    public IAsyncRelayCommand SaveCommand { get; }

    public IRelayCommand ImportLegacyConfigurationCommand { get; }

    public IAsyncRelayCommand TestPlcConnectionCommand { get; }

    public IAsyncRelayCommand ToggleWearPartMonitoringCommand { get; }

    public event EventHandler? ImportLegacyConfigurationRequested;

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(IsNotBusy));
                NotifyOperationStateChanged();
            }
        }
    }

    public bool IsNotBusy => !IsBusy;

    public bool IsDirty
    {
        get => _isDirty;
        private set
        {
            if (SetProperty(ref _isDirty, value))
            {
                SaveCommand.NotifyCanExecuteChanged();
                OnPropertyChanged(nameof(IsSaveClientAppInfoEnabled));
            }
        }
    }

    public bool IsSiemensRackVisible => IsSiemensPlc(PlcProtocolType);

    public bool IsSiemensSlotVisible => IsSiemensPlc(PlcProtocolType);

    public bool IsStringReverseVisible => SupportsStringReverse(PlcProtocolType);

    public bool IsWearPartMonitoringEnabled
    {
        get => _isWearPartMonitoringEnabled;
        private set
        {
            if (SetProperty(ref _isWearPartMonitoringEnabled, value))
            {
                OnPropertyChanged(nameof(WearPartMonitoringButtonText));
                OnPropertyChanged(nameof(WearPartMonitoringStatusText));
                OnPropertyChanged(nameof(WearPartMonitoringStatusBackground));
                OnPropertyChanged(nameof(IsPlcParametersEditable));
                OnPropertyChanged(nameof(IsImportLegacyConfigurationEnabled));
                OnPropertyChanged(nameof(IsTestPlcConnectionEnabled));
                ImportLegacyConfigurationCommand.NotifyCanExecuteChanged();
                TestPlcConnectionCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string WearPartMonitoringButtonText => IsWearPartMonitoringEnabled
        ? LocalizedText.Get("ViewModels.ClientAppInfoVm.StopWearPartMonitoring")
        : LocalizedText.Get("ViewModels.ClientAppInfoVm.StartWearPartMonitoring");

    public string WearPartMonitoringStatusText => IsWearPartMonitoringEnabled
        ? LocalizedText.Get("ViewModels.ClientAppInfoVm.WearPartMonitoringEnabledStatus")
        : LocalizedText.Get("ViewModels.ClientAppInfoVm.WearPartMonitoringDisabledStatus");

    public Brush WearPartMonitoringStatusBackground => IsWearPartMonitoringEnabled
        ? Brushes.ForestGreen
        : Brushes.DimGray;

    public bool IsPlcConnected => _plcConnectionStatusService.Current.Status == PlcStartupConnectionStatus.Connected;

    public bool IsSaveClientAppInfoEnabled => CanSaveCommand();

    public bool IsImportLegacyConfigurationEnabled => CanImportLegacyConfigurationCommand();

    public bool IsTestPlcConnectionEnabled => CanTestPlcConnectionCommand();

    public bool IsToggleWearPartMonitoringEnabled => CanToggleWearPartMonitoringCommand();

    public bool IsPlcParametersEditable => !IsWearPartMonitoringEnabled;

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string SiteCode
    {
        get => _siteCode;
        set
        {
            if (SetProperty(ref _siteCode, value))
            {
                UpdateFactoryOptions();
                UpdateDirtyState();
            }
        }
    }

    public string FactoryCode
    {
        get => _factoryCode;
        set
        {
            if (SetProperty(ref _factoryCode, value))
            {
                EnsureOption(FactoryOptions, value);
                UpdateDirtyState();
            }
        }
    }

    public string AreaCode
    {
        get => _areaCode;
        set
        {
            if (SetProperty(ref _areaCode, value))
            {
                EnsureOption(AreaOptions, value);
                UpdateDirtyState();
            }
        }
    }

    public string ProcedureCode
    {
        get => _procedureCode;
        set
        {
            if (SetProperty(ref _procedureCode, value))
            {
                EnsureOption(ProcedureOptions, value);
                UpdateDirtyState();
            }
        }
    }

    public string EquipmentCode
    {
        get => _equipmentCode;
        set
        {
            if (SetProperty(ref _equipmentCode, value))
            {
                UpdateDirtyState();
            }
        }
    }

    public string ResourceNumber
    {
        get => _resourceNumber;
        set
        {
            if (SetProperty(ref _resourceNumber, value))
            {
                UpdateDirtyState();
            }
        }
    }

    public string PlcProtocolType
    {
        get => _plcProtocolType;
        set
        {
            if (SetProperty(ref _plcProtocolType, value))
            {
                OnPropertyChanged(nameof(IsSiemensRackVisible));
                OnPropertyChanged(nameof(IsSiemensSlotVisible));
                OnPropertyChanged(nameof(IsStringReverseVisible));
                UpdateDirtyState();
            }
        }
    }

    public string PlcIpAddress
    {
        get => _plcIpAddress;
        set
        {
            if (SetProperty(ref _plcIpAddress, value))
            {
                UpdateDirtyState();
            }
        }
    }

    public string PlcPort
    {
        get => _plcPort;
        set
        {
            if (SetProperty(ref _plcPort, value))
            {
                UpdateDirtyState();
            }
        }
    }

    public string ShutdownPointAddress
    {
        get => _shutdownPointAddress;
        set
        {
            if (SetProperty(ref _shutdownPointAddress, value))
            {
                UpdateDirtyState();
            }
        }
    }

    public bool EnableCutterMesValidation
    {
        get => _enableCutterMesValidation;
        set
        {
            if (SetProperty(ref _enableCutterMesValidation, value))
            {
                UpdateDirtyState();
            }
        }
    }

    public string CutterMesWsdl
    {
        get => _cutterMesWsdl;
        set
        {
            if (SetProperty(ref _cutterMesWsdl, value))
            {
                UpdateDirtyState();
            }
        }
    }

    public string CutterMesUser
    {
        get => _cutterMesUser;
        set
        {
            if (SetProperty(ref _cutterMesUser, value))
            {
                UpdateDirtyState();
            }
        }
    }

    public string CutterMesPassword
    {
        get => _cutterMesPassword;
        set
        {
            if (SetProperty(ref _cutterMesPassword, value))
            {
                UpdateDirtyState();
            }
        }
    }

    public string CutterMesSite
    {
        get => _cutterMesSite;
        set
        {
            if (SetProperty(ref _cutterMesSite, value))
            {
                UpdateDirtyState();
            }
        }
    }

    public string SiemensRack
    {
        get => _siemensRack;
        set
        {
            if (SetProperty(ref _siemensRack, value))
            {
                UpdateDirtyState();
            }
        }
    }

    public string SiemensSlot
    {
        get => _siemensSlot;
        set
        {
            if (SetProperty(ref _siemensSlot, value))
            {
                UpdateDirtyState();
            }
        }
    }

    public bool IsStringReverse
    {
        get => _isStringReverse;
        set
        {
            if (SetProperty(ref _isStringReverse, value))
            {
                UpdateDirtyState();
            }
        }
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_isInitialized)
        {
            return;
        }

        IsBusy = true;
        using var _ = _uiBusyService.Enter(LocalizedText.Get("ViewModels.ClientAppInfoVm.Loading"));
        await _uiDispatcher.RenderAsync().ConfigureAwait(true);
        try
        {
            await LoadSelectionOptionsAsync(cancellationToken).ConfigureAwait(true);
            await LoadSiteFactoryOptionsAsync(cancellationToken).ConfigureAwait(true);
            IsWearPartMonitoringEnabled = await _wearPartMonitoringControlService.GetIsEnabledAsync(cancellationToken).ConfigureAwait(true);
            var model = await _clientAppInfoService.GetAsync(cancellationToken).ConfigureAwait(true);
            await ApplyLocalizedAsync(model, cancellationToken).ConfigureAwait(true);
            _isInitialized = true;
            SetLocalizedStatusMessage(() => string.IsNullOrWhiteSpace(model.ResourceNumber)
                ? LocalizedText.Get("ViewModels.ClientAppInfoVm.PromptComplete")
                : LocalizedText.Get("ViewModels.ClientAppInfoVm.Loaded"));
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanSaveCommand()
    {
        return !IsBusy && IsDirty;
    }

    private bool CanImportLegacyConfigurationCommand()
    {
        return !IsBusy && !IsWearPartMonitoringEnabled;
    }

    private bool CanTestPlcConnectionCommand()
    {
        return !IsBusy && !IsWearPartMonitoringEnabled && HasRequiredClientAppInfo();
    }

    private bool CanToggleWearPartMonitoringCommand()
    {
        return !IsBusy && HasRequiredClientAppInfo();
    }

    private async Task SaveAsync()
    {
        IsBusy = true;
        SetLocalizedStatusMessage(() => LocalizedText.Get("ViewModels.ClientAppInfoVm.Saving"));
        using var _ = _uiBusyService.Enter(LocalizedText.Get("ViewModels.ClientAppInfoVm.Saving"));
        await _uiDispatcher.RenderAsync().ConfigureAwait(true);

        try
        {
            var request = BuildSaveRequest();
            var saved = await _clientAppInfoService.SaveAsync(request).ConfigureAwait(true);
            await ApplyLocalizedAsync(saved).ConfigureAwait(true);
            SetLocalizedStatusMessage(() => LocalizedText.Get("ViewModels.ClientAppInfoVm.Saved"));
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

    public async Task<LegacyConfigurationImportResult> ImportLegacyConfigurationAsync(string legacyDatabasePath, CancellationToken cancellationToken = default)
    {
        IsBusy = true;
        SetLocalizedStatusMessage(() => LocalizedText.Get("ViewModels.ClientAppInfoVm.ImportingLegacyConfiguration"));
        using var _ = _uiBusyService.Enter(LocalizedText.Get("ViewModels.ClientAppInfoVm.ImportingLegacyConfiguration"));
        await _uiDispatcher.RenderAsync().ConfigureAwait(true);

        try
        {
            var result = await _legacyConfigurationImportService.ImportAsync(legacyDatabasePath, cancellationToken).ConfigureAwait(true);
            await ApplyLocalizedAsync(result.ClientAppInfo, cancellationToken).ConfigureAwait(true);
            SetLocalizedStatusMessage(() => LocalizedText.Format("ViewModels.ClientAppInfoVm.ImportedLegacyConfiguration", result.ResourceNumber));
            return result;
        }
        catch (Exception ex)
        {
            SetRawStatusMessage(ex.Message);
            throw;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void NotifyLegacyConfigurationImportCanceled()
    {
        SetLocalizedStatusMessage(() => LocalizedText.Get("ViewModels.ClientAppInfoVm.ImportCanceled"));
    }

    private async Task TestPlcConnectionAsync()
    {
        IsBusy = true;
        SetLocalizedStatusMessage(() => LocalizedText.Get("ViewModels.ClientAppInfoVm.TestingPlcConnection"));
        using var _ = _uiBusyService.Enter(LocalizedText.Get("ViewModels.ClientAppInfoVm.TestingPlcConnection"));
        await _uiDispatcher.RenderAsync().ConfigureAwait(true);

        try
        {
            var result = await _plcConnectionTestService.TestAsync(BuildCurrentModel()).ConfigureAwait(true);
            SetRawStatusMessage(result.Message);
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

    private async Task ToggleWearPartMonitoringAsync()
    {
        IsBusy = true;
        SetLocalizedStatusMessage(() => IsWearPartMonitoringEnabled
            ? LocalizedText.Get("ViewModels.ClientAppInfoVm.StoppingWearPartMonitoring")
            : LocalizedText.Get("ViewModels.ClientAppInfoVm.StartingWearPartMonitoring"));
        using var _ = _uiBusyService.Enter(StatusMessage);
        await _uiDispatcher.RenderAsync().ConfigureAwait(true);

        try
        {
            if (IsWearPartMonitoringEnabled)
            {
                await _wearPartMonitoringControlService.DisableAsync().ConfigureAwait(true);
                IsWearPartMonitoringEnabled = false;
                SetLocalizedStatusMessage(() => LocalizedText.Get("ViewModels.ClientAppInfoVm.WearPartMonitoringStopped"));
            }
            else
            {
                await _wearPartMonitoringControlService.EnableAsync().ConfigureAwait(true);
                IsWearPartMonitoringEnabled = true;
                SetLocalizedStatusMessage(() => LocalizedText.Get("ViewModels.ClientAppInfoVm.WearPartMonitoringStarted"));
            }
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

    private void RequestImportLegacyConfiguration()
    {
        ImportLegacyConfigurationRequested?.Invoke(this, EventArgs.Empty);
    }

    private ClientAppInfoModel BuildCurrentModel()
    {
        var request = BuildSaveRequest();
        return new ClientAppInfoModel
        {
            Id = request.Id,
            SiteCode = request.SiteCode,
            FactoryCode = request.FactoryCode,
            AreaCode = request.AreaCode,
            ProcedureCode = request.ProcedureCode,
            EquipmentCode = request.EquipmentCode,
            ResourceNumber = request.ResourceNumber,
            PlcProtocolType = request.PlcProtocolType,
            PlcIpAddress = request.PlcIpAddress,
            PlcPort = request.PlcPort,
            ShutdownPointAddress = request.ShutdownPointAddress,
            EnableCutterMesValidation = request.EnableCutterMesValidation,
            CutterMesWsdl = request.CutterMesWsdl,
            CutterMesUser = request.CutterMesUser,
            CutterMesPassword = request.CutterMesPassword,
            CutterMesSite = request.CutterMesSite,
            SiemensRack = request.SiemensRack,
            SiemensSlot = request.SiemensSlot,
            IsStringReverse = request.IsStringReverse
        };
    }

    private ClientAppInfoSaveRequest BuildSaveRequest()
    {
        if (!int.TryParse(PlcPort?.Trim(), out var plcPort))
        {
            throw new UserFriendlyException(LocalizedText.Get("ViewModels.ClientAppInfoVm.PlcPortInvalid"));
        }

        var siemensRack = 0;
        if (IsSiemensRackVisible && (!int.TryParse(SiemensRack?.Trim(), out siemensRack) || siemensRack < 0 || siemensRack > 255))
        {
            throw new UserFriendlyException(LocalizedText.Get("ViewModels.ClientAppInfoVm.PlcRackInvalid"));
        }

        var siemensSlot = 0;
        if (IsSiemensSlotVisible && (!int.TryParse(SiemensSlot?.Trim(), out siemensSlot) || siemensSlot < 0 || siemensSlot > 255))
        {
            throw new UserFriendlyException(LocalizedText.Get("ViewModels.ClientAppInfoVm.PlcSlotInvalid"));
        }

        return new ClientAppInfoSaveRequest
        {
            Id = _clientAppConfigurationId,
            SiteCode = SiteCode,
            FactoryCode = FactoryCode,
            AreaCode = AreaCode,
            ProcedureCode = ProcedureCode,
            EquipmentCode = EquipmentCode,
            ResourceNumber = ResourceNumber,
            PlcProtocolType = PlcProtocolType,
            PlcIpAddress = PlcIpAddress,
            PlcPort = plcPort,
            ShutdownPointAddress = ShutdownPointAddress,
            EnableCutterMesValidation = EnableCutterMesValidation,
            CutterMesWsdl = CutterMesWsdl,
            CutterMesUser = CutterMesUser,
            CutterMesPassword = CutterMesPassword,
            CutterMesSite = CutterMesSite,
            SiemensRack = siemensRack,
            SiemensSlot = siemensSlot,
            IsStringReverse = IsStringReverseVisible && IsStringReverse
        };
    }

    private async Task ApplyLocalizedAsync(ClientAppInfoModel model, CancellationToken cancellationToken = default)
    {
        model.AreaCode = await _selectionOptionsProvider.MapAreaOptionAsync(model.AreaCode, CultureInfo.CurrentUICulture.Name, cancellationToken).ConfigureAwait(true);
        model.ProcedureCode = await _selectionOptionsProvider.MapProcedureOptionAsync(model.ProcedureCode, CultureInfo.CurrentUICulture.Name, cancellationToken).ConfigureAwait(true);
        Apply(model);
    }

    private void Apply(ClientAppInfoModel model)
    {
        _isUpdatingState = true;
        try
        {
            _clientAppConfigurationId = model.Id;
            SiteCode = model.SiteCode;
            UpdateFactoryOptions();
            FactoryCode = ResolveFactoryCode(model.FactoryCode);
            AreaCode = ResolveAreaCode(model.AreaCode);
            ProcedureCode = ResolveProcedureCode(model.ProcedureCode);
            EquipmentCode = model.EquipmentCode;
            ResourceNumber = model.ResourceNumber;
            PlcProtocolType = model.PlcProtocolType;
            PlcIpAddress = model.PlcIpAddress;
            PlcPort = model.PlcPort.ToString();
            ShutdownPointAddress = model.ShutdownPointAddress;
            EnableCutterMesValidation = model.EnableCutterMesValidation;
            CutterMesWsdl = model.CutterMesWsdl;
            CutterMesUser = model.CutterMesUser;
            CutterMesPassword = model.CutterMesPassword;
            CutterMesSite = model.CutterMesSite;
            SiemensRack = model.SiemensRack.ToString();
            SiemensSlot = model.SiemensSlot.ToString();
            IsStringReverse = model.IsStringReverse;
            _originalSnapshot = CaptureSnapshot();
            IsDirty = false;
        }
        finally
        {
            _isUpdatingState = false;
        }
    }

    private ClientAppInfoSnapshot CaptureSnapshot()
    {
        return new ClientAppInfoSnapshot(
            Normalize(SiteCode),
            Normalize(FactoryCode),
            Normalize(AreaCode),
            Normalize(ProcedureCode),
            Normalize(EquipmentCode),
            Normalize(ResourceNumber),
            Normalize(PlcProtocolType),
            Normalize(PlcIpAddress),
            Normalize(PlcPort),
            Normalize(ShutdownPointAddress),
            EnableCutterMesValidation,
            Normalize(CutterMesWsdl),
            Normalize(CutterMesUser),
            Normalize(CutterMesPassword),
            Normalize(CutterMesSite),
            Normalize(SiemensRack),
            Normalize(SiemensSlot),
            IsStringReverse);
    }

    private void UpdateDirtyState()
    {
        if (_isUpdatingState)
        {
            return;
        }

        IsDirty = _originalSnapshot != CaptureSnapshot();
        OnPropertyChanged(nameof(IsTestPlcConnectionEnabled));
        OnPropertyChanged(nameof(IsToggleWearPartMonitoringEnabled));
        TestPlcConnectionCommand.NotifyCanExecuteChanged();
        ToggleWearPartMonitoringCommand.NotifyCanExecuteChanged();
    }

    private bool HasRequiredClientAppInfo()
    {
        return !string.IsNullOrWhiteSpace(Normalize(SiteCode))
            && !string.IsNullOrWhiteSpace(Normalize(FactoryCode))
            && !string.IsNullOrWhiteSpace(Normalize(AreaCode))
            && !string.IsNullOrWhiteSpace(Normalize(ProcedureCode))
            && !string.IsNullOrWhiteSpace(Normalize(EquipmentCode))
            && !string.IsNullOrWhiteSpace(Normalize(ResourceNumber))
            && !string.IsNullOrWhiteSpace(Normalize(PlcProtocolType))
            && !string.IsNullOrWhiteSpace(Normalize(PlcIpAddress))
            && !string.IsNullOrWhiteSpace(Normalize(ShutdownPointAddress));
    }

    private async Task LoadSiteFactoryOptionsAsync(CancellationToken cancellationToken)
    {
        var path = Path.Combine(PortableDataPaths.SettingsDirectory, "site-factory.json");
        SiteOptions.Clear();
        FactoryOptions.Clear();
        _siteFactoryOptions.Clear();

        if (!File.Exists(path))
        {
            return;
        }

        var json = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(true);
        var document = JsonSerializer.Deserialize<SiteFactoryDocument>(json) ?? new SiteFactoryDocument();
        foreach (var item in document.Factories)
        {
            _siteFactoryOptions.Add(item);
            SiteOptions.Add(new SiteOption
            {
                Code = item.Site?.Trim() ?? string.Empty,
                DisplayName = string.IsNullOrWhiteSpace(item.SiteName)
                    ? item.Site?.Trim() ?? string.Empty
                    : $"{item.Site?.Trim()} - {item.SiteName.Trim()}"
            });
        }
    }

    private async Task LoadSelectionOptionsAsync(CancellationToken cancellationToken)
    {
        var options = await _selectionOptionsProvider.GetAsync(cancellationToken).ConfigureAwait(true);

        AreaOptions.Clear();
        foreach (var area in options.AreaOptions)
        {
            AreaOptions.Add(area);
        }

        ProcedureOptions.Clear();
        foreach (var procedure in options.ProcedureOptions)
        {
            ProcedureOptions.Add(procedure);
        }
    }

    private void UpdateFactoryOptions()
    {
        FactoryOptions.Clear();

        var siteCode = Normalize(SiteCode);
        if (string.IsNullOrWhiteSpace(siteCode))
        {
            if (!_isUpdatingState)
            {
                FactoryCode = string.Empty;
            }

            return;
        }

        foreach (var factory in _siteFactoryOptions
                     .Where(x => string.Equals(x.Site?.Trim(), siteCode, StringComparison.OrdinalIgnoreCase))
                     .SelectMany(x => x.FactoryNames ?? [])
                     .Where(x => !string.IsNullOrWhiteSpace(x))
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            FactoryOptions.Add(factory.Trim());
        }

        if (!string.IsNullOrWhiteSpace(FactoryCode) && !FactoryOptions.Contains(FactoryCode))
        {
            if (_isUpdatingState)
            {
                FactoryOptions.Add(FactoryCode);
            }
            else
            {
                FactoryCode = string.Empty;
            }
        }
    }

    private static bool IsSiemensPlc(string? plcProtocolType)
    {
        return string.Equals(plcProtocolType?.Trim(), nameof(WearPartsControl.ApplicationServices.PlcService.PlcProtocolType.SiemensS1500), StringComparison.Ordinal)
            || string.Equals(plcProtocolType?.Trim(), nameof(WearPartsControl.ApplicationServices.PlcService.PlcProtocolType.SiemensS1200), StringComparison.Ordinal);
    }

    private static bool SupportsStringReverse(string? plcProtocolType)
    {
        if (string.IsNullOrWhiteSpace(plcProtocolType))
        {
            return false;
        }

        return string.Equals(plcProtocolType.Trim(), nameof(WearPartsControl.ApplicationServices.PlcService.PlcProtocolType.ModbusTcp), StringComparison.Ordinal)
            || plcProtocolType.StartsWith("Inovance", StringComparison.Ordinal);
    }

    private static void EnsureOption(ICollection<string> options, string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || options.Contains(value.Trim()))
        {
            return;
        }

        options.Add(value.Trim());
    }

    private void OnPlcConnectionStatusChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(IPlcConnectionStatusService.Current))
        {
            return;
        }

        _ = RefreshPlcDependentUiStateAsync();
    }

    private async Task RefreshPlcDependentUiStateAsync()
    {
        bool? monitoringEnabled = null;

        try
        {
            monitoringEnabled = await _wearPartMonitoringControlService.GetIsEnabledAsync().ConfigureAwait(false);
        }
        catch
        {
            // 刷新监控状态失败不应阻断 PLC 连接状态自身的 UI 刷新。
        }

        await _uiDispatcher.RunAsync(() =>
        {
            if (monitoringEnabled.HasValue)
            {
                IsWearPartMonitoringEnabled = monitoringEnabled.Value;
            }

            OnPropertyChanged(nameof(IsPlcConnected));
            OnPropertyChanged(nameof(IsImportLegacyConfigurationEnabled));
            OnPropertyChanged(nameof(IsTestPlcConnectionEnabled));
            OnPropertyChanged(nameof(IsToggleWearPartMonitoringEnabled));
            ImportLegacyConfigurationCommand.NotifyCanExecuteChanged();
            TestPlcConnectionCommand.NotifyCanExecuteChanged();
            ToggleWearPartMonitoringCommand.NotifyCanExecuteChanged();
        }).ConfigureAwait(false);
    }

    private void NotifyOperationStateChanged()
    {
        SaveCommand.NotifyCanExecuteChanged();
        ImportLegacyConfigurationCommand.NotifyCanExecuteChanged();
        TestPlcConnectionCommand.NotifyCanExecuteChanged();
        ToggleWearPartMonitoringCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(IsSaveClientAppInfoEnabled));
        OnPropertyChanged(nameof(IsImportLegacyConfigurationEnabled));
        OnPropertyChanged(nameof(IsTestPlcConnectionEnabled));
        OnPropertyChanged(nameof(IsToggleWearPartMonitoringEnabled));
    }

    private static string Normalize(string? value)
    {
        return value?.Trim() ?? string.Empty;
    }

    private string ResolveAreaCode(string? areaCode)
    {
        var normalized = Normalize(areaCode);
        if (!string.IsNullOrWhiteSpace(normalized))
        {
            return normalized;
        }

        return AreaOptions.FirstOrDefault() ?? string.Empty;
    }

    private string ResolveProcedureCode(string? procedureCode)
    {
        var normalized = Normalize(procedureCode);
        if (!string.IsNullOrWhiteSpace(normalized))
        {
            return normalized;
        }

        return ProcedureOptions.FirstOrDefault() ?? string.Empty;
    }

    private string ResolveFactoryCode(string? factoryCode)
    {
        var normalized = Normalize(factoryCode);
        if (!string.IsNullOrWhiteSpace(normalized))
        {
            return normalized;
        }

        return FactoryOptions.FirstOrDefault() ?? string.Empty;
    }

    protected override void OnLocalizationRefreshed()
    {
        OnPropertyChanged(nameof(WearPartMonitoringButtonText));
        OnPropertyChanged(nameof(WearPartMonitoringStatusText));

        if (_statusMessageFactory is not null)
        {
            StatusMessage = _statusMessageFactory();
        }

        if (_isInitialized)
        {
            _ = RefreshLocalizedSelectionOptionsAsync();
        }
    }

    private async Task RefreshLocalizedSelectionOptionsAsync()
    {
        var selectedAreaCode = AreaCode;
        var selectedProcedureCode = ProcedureCode;
        var options = await _selectionOptionsProvider.GetAsync().ConfigureAwait(false);
        var localizedAreaCode = await _selectionOptionsProvider.MapAreaOptionAsync(selectedAreaCode, CultureInfo.CurrentUICulture.Name).ConfigureAwait(false);
        var localizedProcedureCode = await _selectionOptionsProvider.MapProcedureOptionAsync(selectedProcedureCode, CultureInfo.CurrentUICulture.Name).ConfigureAwait(false);

        await _uiDispatcher.RunAsync(() =>
        {
            _isUpdatingState = true;
            try
            {
                AreaOptions.Clear();
                foreach (var area in options.AreaOptions)
                {
                    AreaOptions.Add(area);
                }

                ProcedureOptions.Clear();
                foreach (var procedure in options.ProcedureOptions)
                {
                    ProcedureOptions.Add(procedure);
                }

                EnsureOption(AreaOptions, localizedAreaCode);
                EnsureOption(ProcedureOptions, localizedProcedureCode);
                AreaCode = ResolveAreaCode(localizedAreaCode);
                ProcedureCode = ResolveProcedureCode(localizedProcedureCode);
            }
            finally
            {
                _isUpdatingState = false;
            }
        }).ConfigureAwait(false);
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

    private sealed record ClientAppInfoSnapshot(
        string SiteCode,
        string FactoryCode,
        string AreaCode,
        string ProcedureCode,
        string EquipmentCode,
        string ResourceNumber,
        string PlcProtocolType,
        string PlcIpAddress,
        string PlcPort,
        string ShutdownPointAddress,
        bool EnableCutterMesValidation,
        string CutterMesWsdl,
        string CutterMesUser,
        string CutterMesPassword,
        string CutterMesSite,
        string SiemensRack,
        string SiemensSlot,
        bool IsStringReverse)
    {
        public static ClientAppInfoSnapshot Empty { get; } = new(string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, false, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, true);
    }

    public sealed class SiteOption
    {
        public string Code { get; set; } = string.Empty;

        public string DisplayName { get; set; } = string.Empty;
    }

    private sealed class SiteFactoryDocument
    {
        public List<SiteFactoryOption> Factories { get; set; } = new();
    }

    private sealed class SiteFactoryOption
    {
        public string? Site { get; set; }

        public string? SiteName { get; set; }

        public List<string>? FactoryNames { get; set; }
    }
}