using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.Input;
using WearPartsControl.ApplicationServices;
using WearPartsControl.ApplicationServices.ClientAppInfo;
using WearPartsControl.ApplicationServices.Localization;
using WearPartsControl.ApplicationServices.PartServices;

namespace WearPartsControl.ViewModels;

public sealed class WearPartValuePreviewViewModel : LocalizedViewModelBase
{
    private readonly IClientAppInfoService _clientAppInfoService;
    private readonly IWearPartValuePreviewService _wearPartValuePreviewService;
    private readonly IUiDispatcher _uiDispatcher;
    private readonly IUiBusyService _uiBusyService;
    private bool _isBusy;
    private bool _isInitialized;
    private int _normalCount;
    private string _resourceNumber = string.Empty;
    private int _shutdownCount;
    private string _statusMessage = LocalizedText.Get("ViewModels.WearPartValuePreviewVm.PromptLoadCurrent");
    private Func<string>? _statusMessageFactory;
    private int _totalCount;
    private int _warningCount;

    public WearPartValuePreviewViewModel(
        IClientAppInfoService clientAppInfoService,
        IWearPartValuePreviewService wearPartValuePreviewService,
        IUiDispatcher uiDispatcher,
        IUiBusyService uiBusyService)
    {
        _clientAppInfoService = clientAppInfoService;
        _wearPartValuePreviewService = wearPartValuePreviewService;
        _uiDispatcher = uiDispatcher;
        _uiBusyService = uiBusyService;
        RefreshCommand = new AsyncRelayCommand(() => RefreshAsync(CancellationToken.None), CanRefresh);
        SetLocalizedStatusMessage(() => LocalizedText.Get("ViewModels.WearPartValuePreviewVm.PromptLoadCurrent"));
    }

    public ObservableCollection<WearPartValuePreviewRowViewModel> Items { get; } = new();

    public IAsyncRelayCommand RefreshCommand { get; }

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
            Items.Clear();
            ResetSummary();

            if (!clientInfo.Id.HasValue || clientInfo.Id.Value == Guid.Empty || string.IsNullOrWhiteSpace(ResourceNumber))
            {
                SetLocalizedStatusMessage(() => LocalizedText.Get("ViewModels.WearPartValuePreviewVm.ResourceNumberMissing"));
                return;
            }

            var previews = await _wearPartValuePreviewService.GetByResourceNumberAsync(ResourceNumber, cancellationToken).ConfigureAwait(true);
            foreach (var preview in previews)
            {
                Items.Add(new WearPartValuePreviewRowViewModel(preview));
            }

            UpdateSummary(previews);

            SetLocalizedStatusMessage(() => Items.Count == 0
                ? LocalizedText.Format("ViewModels.WearPartValuePreviewVm.PreviewEmpty", ResourceNumber)
                : LocalizedText.Format("ViewModels.WearPartValuePreviewVm.PreviewLoaded", ResourceNumber, Items.Count));
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

    private void ResetSummary()
    {
        TotalCount = 0;
        NormalCount = 0;
        WarningCount = 0;
        ShutdownCount = 0;
    }

    private void UpdateSummary(IReadOnlyCollection<WearPartValuePreviewItem> previews)
    {
        TotalCount = previews.Count;
        NormalCount = previews.Count(item => item.Status == WearPartMonitorStatus.Normal);
        WarningCount = previews.Count(item => item.Status == WearPartMonitorStatus.Warning);
        ShutdownCount = previews.Count(item => item.Status == WearPartMonitorStatus.Shutdown);
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