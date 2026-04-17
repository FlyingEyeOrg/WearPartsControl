using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WearPartsControl.ApplicationServices;
using WearPartsControl.ApplicationServices.ClientAppInfo;
using WearPartsControl.ApplicationServices.PlcService;
using WearPartsControl.Exceptions;

namespace WearPartsControl.ViewModels;

public sealed class ClientAppInfoViewModel : ObservableObject
{
    private readonly IClientAppInfoService _clientAppInfoService;
    private readonly IClientAppInfoSelectionOptionsProvider _selectionOptionsProvider;
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
    private string _siemensSlot = "1";
    private string _statusMessage = "请先完善客户端信息。";
    private bool _isStringReverse = true;

    public ClientAppInfoViewModel(
        IClientAppInfoService clientAppInfoService,
        IClientAppInfoSelectionOptionsProvider selectionOptionsProvider,
        IUiBusyService uiBusyService)
    {
        _clientAppInfoService = clientAppInfoService;
        _selectionOptionsProvider = selectionOptionsProvider;
        _uiBusyService = uiBusyService;
        SaveCommand = new AsyncRelayCommand(SaveAsync, CanSave);

        foreach (var plcProtocolType in Enum.GetNames<PlcProtocolType>())
        {
            PlcProtocolTypes.Add(plcProtocolType);
        }
    }

    public ObservableCollection<SiteOption> SiteOptions { get; } = new();

    public ObservableCollection<string> FactoryOptions { get; } = new();

    public ObservableCollection<string> AreaOptions { get; } = new();

    public ObservableCollection<string> ProcedureOptions { get; } = new();

    public ObservableCollection<string> PlcProtocolTypes { get; } = new();

    public IAsyncRelayCommand SaveCommand { get; }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(IsNotBusy));
                SaveCommand.NotifyCanExecuteChanged();
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
            }
        }
    }

    public bool IsSiemensSlotVisible => IsSiemensPlc(PlcProtocolType);

    public bool IsStringReverseVisible => SupportsStringReverse(PlcProtocolType);

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
        using var _ = _uiBusyService.Enter();
        try
        {
            await LoadSelectionOptionsAsync(cancellationToken).ConfigureAwait(true);
            await LoadSiteFactoryOptionsAsync(cancellationToken).ConfigureAwait(true);
            var model = await _clientAppInfoService.GetAsync(cancellationToken).ConfigureAwait(true);
            Apply(model);
            _isInitialized = true;
            StatusMessage = string.IsNullOrWhiteSpace(model.ResourceNumber)
                ? "请先完善客户端信息。"
                : "客户端信息已加载。";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanSave()
    {
        return !IsBusy && IsDirty;
    }

    private async Task SaveAsync()
    {
        IsBusy = true;
        StatusMessage = "正在保存客户端信息...";
        using var _ = _uiBusyService.Enter();

        try
        {
            var request = BuildSaveRequest();
            var saved = await _clientAppInfoService.SaveAsync(request).ConfigureAwait(true);
            Apply(saved);
            StatusMessage = "客户端信息保存成功。";
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

    private ClientAppInfoSaveRequest BuildSaveRequest()
    {
        if (!int.TryParse(PlcPort?.Trim(), out var plcPort))
        {
            throw new UserFriendlyException("PLC 端口必须是整数。");
        }

        var siemensSlot = 1;
        if (IsSiemensSlotVisible && !int.TryParse(SiemensSlot?.Trim(), out siemensSlot))
        {
            throw new UserFriendlyException("PLC 插槽号必须是整数。");
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
            SiemensSlot = siemensSlot,
            IsStringReverse = IsStringReverseVisible && IsStringReverse
        };
    }

    private void Apply(ClientAppInfoModel model)
    {
        _isUpdatingState = true;
        try
        {
            _clientAppConfigurationId = model.Id;
            SiteCode = model.SiteCode;
            FactoryCode = model.FactoryCode;
            AreaCode = ResolveAreaCode(model.AreaCode);
            ProcedureCode = model.ProcedureCode;
            EquipmentCode = model.EquipmentCode;
            ResourceNumber = model.ResourceNumber;
            PlcProtocolType = model.PlcProtocolType;
            PlcIpAddress = model.PlcIpAddress;
            PlcPort = model.PlcPort.ToString();
            ShutdownPointAddress = model.ShutdownPointAddress;
            SiemensSlot = model.SiemensSlot.ToString();
            IsStringReverse = model.IsStringReverse;

            UpdateFactoryOptions();
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
        string SiemensSlot,
        bool IsStringReverse)
    {
        public static ClientAppInfoSnapshot Empty { get; } = new(string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, true);
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