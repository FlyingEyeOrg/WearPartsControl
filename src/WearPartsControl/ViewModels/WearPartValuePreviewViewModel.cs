using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.Input;
using WearPartsControl.ApplicationServices;
using WearPartsControl.ApplicationServices.ClientAppInfo;
using WearPartsControl.ApplicationServices.Localization;
using WearPartsControl.ApplicationServices.LoginService;
using WearPartsControl.ApplicationServices.PartServices;

namespace WearPartsControl.ViewModels;

public sealed class WearPartValuePreviewViewModel : LocalizedViewModelBase
{
    private const int MinimumAccessLevelForThresholdSync = 4;

    private readonly IClientAppInfoService _clientAppInfoService;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IWearPartValuePreviewService _wearPartValuePreviewService;
    private readonly IUiDispatcher _uiDispatcher;
    private readonly IUiBusyService _uiBusyService;
    private bool _canSyncConfiguredThresholds;
    private bool _isBusy;
    private bool _isInitialized;
    private int _normalCount;
    private string _resourceNumber = string.Empty;
    private WearPartValuePreviewRowViewModel? _selectedItem;
    private int _shutdownCount;
    private string _statusMessage = LocalizedText.Get("ViewModels.WearPartValuePreviewVm.PromptLoadCurrent");
    private Func<string>? _statusMessageFactory;
    private int _totalCount;
    private int _warningCount;

    public WearPartValuePreviewViewModel(
        IClientAppInfoService clientAppInfoService,
        ICurrentUserAccessor currentUserAccessor,
        IWearPartValuePreviewService wearPartValuePreviewService,
        IUiDispatcher uiDispatcher,
        IUiBusyService uiBusyService)
    {
        _clientAppInfoService = clientAppInfoService;
        _currentUserAccessor = currentUserAccessor;
        _wearPartValuePreviewService = wearPartValuePreviewService;
        _uiDispatcher = uiDispatcher;
        _uiBusyService = uiBusyService;
        RefreshCommand = new AsyncRelayCommand(() => RefreshAsync(CancellationToken.None), CanRefresh);
        EditThresholdsCommand = new RelayCommand(RequestThresholdEdit, CanEditThresholds);
        SyncThresholdsCommand = new AsyncRelayCommand(SyncThresholdsAsync, CanSyncThresholds);
        SetLocalizedStatusMessage(() => LocalizedText.Get("ViewModels.WearPartValuePreviewVm.PromptLoadCurrent"));
    }

    public event EventHandler<WearPartValuePreviewRowViewModel>? ThresholdEditRequested;

    public ObservableCollection<WearPartValuePreviewRowViewModel> Items { get; } = new();

    public IAsyncRelayCommand RefreshCommand { get; }

    public IRelayCommand EditThresholdsCommand { get; }

    public IAsyncRelayCommand SyncThresholdsCommand { get; }

    public WearPartValuePreviewRowViewModel? SelectedItem
    {
        get => _selectedItem;
        set
        {
            if (SetProperty(ref _selectedItem, value))
            {
                EditThresholdsCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string ResourceNumber
    {
        get => _resourceNumber;
        private set => SetProperty(ref _resourceNumber, value);
    }

    public int TotalCount
    {
        get => _totalCount;
        private set => SetProperty(ref _totalCount, value);
    }

    public int NormalCount
    {
        get => _normalCount;
        private set => SetProperty(ref _normalCount, value);
    }

    public int WarningCount
    {
        get => _warningCount;
        private set => SetProperty(ref _warningCount, value);
    }

    public int ShutdownCount
    {
        get => _shutdownCount;
        private set => SetProperty(ref _shutdownCount, value);
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
                EditThresholdsCommand.NotifyCanExecuteChanged();
                SyncThresholdsCommand.NotifyCanExecuteChanged();
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

    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        SetLocalizedStatusMessage(() => LocalizedText.Get("ViewModels.WearPartValuePreviewVm.Loading"));
        using var _ = _uiBusyService.Enter(LocalizedText.Get("ViewModels.WearPartValuePreviewVm.Loading"));
        await _uiDispatcher.RenderAsync().ConfigureAwait(true);

        try
        {
            var clientInfo = await _clientAppInfoService.GetAsync(cancellationToken).ConfigureAwait(true);
            ResourceNumber = clientInfo.ResourceNumber?.Trim() ?? string.Empty;
            ApplyPreviews([]);
            UpdateSyncAvailability(false);

            if (!clientInfo.Id.HasValue || clientInfo.Id.Value == Guid.Empty || string.IsNullOrWhiteSpace(ResourceNumber))
            {
                SetLocalizedStatusMessage(() => LocalizedText.Get("ViewModels.WearPartValuePreviewVm.ResourceNumberMissing"));
                return;
            }

            var previews = await _wearPartValuePreviewService.GetByResourceNumberAsync(ResourceNumber, cancellationToken).ConfigureAwait(true);
            ApplyPreviews(previews);
            UpdateSyncAvailability(Items.Count > 0);

            SetLocalizedStatusMessage(() => Items.Count == 0
                ? LocalizedText.Format("ViewModels.WearPartValuePreviewVm.PreviewEmpty", ResourceNumber)
                : LocalizedText.Format("ViewModels.WearPartValuePreviewVm.PreviewLoaded", ResourceNumber, Items.Count));
        }
        catch (Exception ex)
        {
            UpdateSyncAvailability(false);
            SetRawStatusMessage(ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task SyncThresholdsAsync()
    {
        if (IsBusy || string.IsNullOrWhiteSpace(ResourceNumber))
        {
            return;
        }

        IsBusy = true;
        SetLocalizedStatusMessage(() => LocalizedText.Get("ViewModels.WearPartValuePreviewVm.SyncingConfiguredThresholds"));
        using var _ = _uiBusyService.Enter(LocalizedText.Get("ViewModels.WearPartValuePreviewVm.SyncingConfiguredThresholds"));
        await _uiDispatcher.RenderAsync().ConfigureAwait(true);

        try
        {
            var previews = await _wearPartValuePreviewService.SyncConfiguredThresholdsToDeviceAsync(ResourceNumber, CancellationToken.None).ConfigureAwait(true);
            ApplyPreviews(previews);
            UpdateSyncAvailability(Items.Count > 0);
            SetLocalizedStatusMessage(() => Items.Count == 0
                ? LocalizedText.Format("ViewModels.WearPartValuePreviewVm.PreviewEmpty", ResourceNumber)
                : LocalizedText.Format("ViewModels.WearPartValuePreviewVm.SyncCompleted", ResourceNumber, Items.Count));
        }
        catch (Exception ex)
        {
            UpdateSyncAvailability(false);
            SetRawStatusMessage(ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    protected override void OnLocalizationRefreshed()
    {
        foreach (var item in Items)
        {
            item.RefreshLocalizedProperties();
        }

        if (_statusMessageFactory is not null)
        {
            StatusMessage = _statusMessageFactory();
        }
    }

    private bool CanRefresh() => !IsBusy;

    private bool CanEditThresholds()
    {
        return !IsBusy
            && SelectedItem is not null
            && _currentUserAccessor.CurrentUser?.AccessLevel >= MinimumAccessLevelForThresholdSync;
    }

    private bool CanSyncThresholds()
    {
        return !IsBusy
            && _canSyncConfiguredThresholds
            && _currentUserAccessor.CurrentUser?.AccessLevel >= MinimumAccessLevelForThresholdSync;
    }

    private void RequestThresholdEdit()
    {
        if (SelectedItem is null)
        {
            return;
        }

        ThresholdEditRequested?.Invoke(this, SelectedItem);
    }

    private void ResetSummary()
    {
        TotalCount = 0;
        NormalCount = 0;
        WarningCount = 0;
        ShutdownCount = 0;
    }

    private void ApplyPreviews(IReadOnlyCollection<WearPartValuePreviewItem> previews)
    {
        Items.Clear();
        SelectedItem = null;
        ResetSummary();

        foreach (var preview in previews)
        {
            Items.Add(new WearPartValuePreviewRowViewModel(preview));
        }

        UpdateSummary(previews);
    }

    private void UpdateSummary(IReadOnlyCollection<WearPartValuePreviewItem> previews)
    {
        TotalCount = previews.Count;
        NormalCount = previews.Count(item => item.Status == WearPartMonitorStatus.Normal);
        WarningCount = previews.Count(item => item.Status == WearPartMonitorStatus.Warning);
        ShutdownCount = previews.Count(item => item.Status == WearPartMonitorStatus.Shutdown);
    }

    private void UpdateSyncAvailability(bool canSyncConfiguredThresholds)
    {
        _canSyncConfiguredThresholds = canSyncConfiguredThresholds;
        SyncThresholdsCommand.NotifyCanExecuteChanged();
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