using System.Globalization;
using CommunityToolkit.Mvvm.Input;
using WearPartsControl.ApplicationServices;
using WearPartsControl.ApplicationServices.Localization;
using WearPartsControl.ApplicationServices.LoginService;
using WearPartsControl.ApplicationServices.PartServices;
using WearPartsControl.Exceptions;

namespace WearPartsControl.ViewModels;

public sealed class WearPartThresholdWindowViewModel : LocalizedViewModelBase
{
    private const int MinimumAccessLevelForThresholdEdit = 4;

    private readonly IWearPartThresholdService _wearPartThresholdService;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IUiDispatcher _uiDispatcher;
    private readonly IUiBusyService _uiBusyService;
    private Guid _wearPartDefinitionId;
    private bool _isBusy;
    private string _resourceNumber = string.Empty;
    private string _partName = string.Empty;
    private string _lifetimeType = string.Empty;
    private string _warningLifetimeThreshold = string.Empty;
    private string _shutdownLifetimeThreshold = string.Empty;
    private string _plcWarningLifetimeThreshold = string.Empty;
    private string _plcShutdownLifetimeThreshold = string.Empty;
    private string _statusMessage = LocalizedText.Get("ViewModels.WearPartThresholdVm.PromptLoad");
    private Func<string>? _statusMessageFactory;

    public WearPartThresholdWindowViewModel(
        IWearPartThresholdService wearPartThresholdService,
        ICurrentUserAccessor currentUserAccessor,
        IUiDispatcher uiDispatcher,
        IUiBusyService uiBusyService)
    {
        _wearPartThresholdService = wearPartThresholdService;
        _currentUserAccessor = currentUserAccessor;
        _uiDispatcher = uiDispatcher;
        _uiBusyService = uiBusyService;

        SaveCommand = new AsyncRelayCommand(SaveAsync, CanSave);
        RefreshPlcCommand = new AsyncRelayCommand(RefreshPlcAsync, CanRefreshPlc);
        OverwritePlcCommand = new AsyncRelayCommand(OverwritePlcAsync, CanOverwritePlc);
        CancelCommand = new RelayCommand(Cancel);
        SetLocalizedStatusMessage(() => LocalizedText.Get("ViewModels.WearPartThresholdVm.PromptLoad"));
    }

    public event EventHandler<bool?>? RequestClose;

    public IAsyncRelayCommand SaveCommand { get; }

    public IAsyncRelayCommand RefreshPlcCommand { get; }

    public IAsyncRelayCommand OverwritePlcCommand { get; }

    public IRelayCommand CancelCommand { get; }

    public string ResourceNumber
    {
        get => _resourceNumber;
        private set => SetProperty(ref _resourceNumber, value);
    }

    public string PartName
    {
        get => _partName;
        private set => SetProperty(ref _partName, value);
    }

    public string LifetimeType
    {
        get => _lifetimeType;
        private set => SetProperty(ref _lifetimeType, value);
    }

    public string WarningLifetimeThreshold
    {
        get => _warningLifetimeThreshold;
        set
        {
            if (SetProperty(ref _warningLifetimeThreshold, value))
            {
                SaveCommand.NotifyCanExecuteChanged();
                OverwritePlcCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string ShutdownLifetimeThreshold
    {
        get => _shutdownLifetimeThreshold;
        set
        {
            if (SetProperty(ref _shutdownLifetimeThreshold, value))
            {
                SaveCommand.NotifyCanExecuteChanged();
                OverwritePlcCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string PlcWarningLifetimeThreshold
    {
        get => _plcWarningLifetimeThreshold;
        private set => SetProperty(ref _plcWarningLifetimeThreshold, value);
    }

    public string PlcShutdownLifetimeThreshold
    {
        get => _plcShutdownLifetimeThreshold;
        private set => SetProperty(ref _plcShutdownLifetimeThreshold, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(IsNotBusy));
                SaveCommand.NotifyCanExecuteChanged();
                RefreshPlcCommand.NotifyCanExecuteChanged();
                OverwritePlcCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool IsNotBusy => !IsBusy;

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public async Task InitializeAsync(Guid wearPartDefinitionId, CancellationToken cancellationToken = default)
    {
        _wearPartDefinitionId = wearPartDefinitionId;
        await LoadAsync(cancellationToken).ConfigureAwait(true);
    }

    protected override void OnLocalizationRefreshed()
    {
        if (_statusMessageFactory is not null)
        {
            StatusMessage = _statusMessageFactory();
        }
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        if (_wearPartDefinitionId == Guid.Empty || IsBusy)
        {
            return;
        }

        IsBusy = true;
        SetLocalizedStatusMessage(() => LocalizedText.Get("ViewModels.WearPartThresholdVm.Loading"));
        using var _ = _uiBusyService.Enter(LocalizedText.Get("ViewModels.WearPartThresholdVm.Loading"));
        await _uiDispatcher.RenderAsync().ConfigureAwait(true);

        try
        {
            var profile = await _wearPartThresholdService.GetProfileAsync(_wearPartDefinitionId, cancellationToken).ConfigureAwait(true);
            ApplyProfile(profile);

            try
            {
                var plcSnapshot = await _wearPartThresholdService.ReadPlcThresholdsAsync(_wearPartDefinitionId, cancellationToken).ConfigureAwait(true);
                ApplyPlcSnapshot(plcSnapshot);
                SetLocalizedStatusMessage(() => LocalizedText.Format("ViewModels.WearPartThresholdVm.Loaded", ResourceNumber, PartName));
            }
            catch (Exception ex)
            {
                PlcWarningLifetimeThreshold = LocalizedText.Get("WearPartThresholdWindowView.NotAvailableText");
                PlcShutdownLifetimeThreshold = LocalizedText.Get("WearPartThresholdWindowView.NotAvailableText");
                SetRawStatusMessage(ex.Message);
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

    private async Task SaveAsync()
    {
        await ExecuteBusyAsync(
            async cancellationToken =>
            {
                var request = BuildUpdateRequest();
                var profile = await _wearPartThresholdService.UpdateThresholdsAsync(request, cancellationToken).ConfigureAwait(true);
                ApplyProfile(profile);
                SetLocalizedStatusMessage(() => LocalizedText.Format("ViewModels.WearPartThresholdVm.Saved", PartName));
            },
            () => LocalizedText.Get("ViewModels.WearPartThresholdVm.Saving")).ConfigureAwait(true);
    }

    private async Task RefreshPlcAsync()
    {
        await ExecuteBusyAsync(
            async cancellationToken =>
            {
                var snapshot = await _wearPartThresholdService.ReadPlcThresholdsAsync(_wearPartDefinitionId, cancellationToken).ConfigureAwait(true);
                ApplyPlcSnapshot(snapshot);
                SetLocalizedStatusMessage(() => LocalizedText.Format("ViewModels.WearPartThresholdVm.PlcLoaded", PartName));
            },
            () => LocalizedText.Get("ViewModels.WearPartThresholdVm.LoadingPlc")).ConfigureAwait(true);
    }

    private async Task OverwritePlcAsync()
    {
        await ExecuteBusyAsync(
            async cancellationToken =>
            {
                var request = BuildUpdateRequest();
                var profile = await _wearPartThresholdService.UpdateThresholdsAsync(request, cancellationToken).ConfigureAwait(true);
                ApplyProfile(profile);
                var snapshot = await _wearPartThresholdService.OverwritePlcThresholdsAsync(_wearPartDefinitionId, cancellationToken).ConfigureAwait(true);
                ApplyPlcSnapshot(snapshot);
                SetLocalizedStatusMessage(() => LocalizedText.Format("ViewModels.WearPartThresholdVm.PlcOverwritten", PartName));
            },
            () => LocalizedText.Get("ViewModels.WearPartThresholdVm.OverwritingPlc")).ConfigureAwait(true);
    }

    private async Task ExecuteBusyAsync(Func<CancellationToken, Task> action, Func<string> busyMessageFactory)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        SetLocalizedStatusMessage(busyMessageFactory);
        using var _ = _uiBusyService.Enter(busyMessageFactory());
        await _uiDispatcher.RenderAsync().ConfigureAwait(true);

        try
        {
            await action(CancellationToken.None).ConfigureAwait(true);
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

    private WearPartThresholdUpdateRequest BuildUpdateRequest()
    {
        EnsureEditable();

        return new WearPartThresholdUpdateRequest
        {
            WearPartDefinitionId = _wearPartDefinitionId,
            WarningLifetimeThreshold = ParseThreshold(WarningLifetimeThreshold),
            ShutdownLifetimeThreshold = ParseThreshold(ShutdownLifetimeThreshold)
        };
    }

    private void EnsureEditable()
    {
        var currentUser = _currentUserAccessor.CurrentUser;
        if (currentUser is null || currentUser.AccessLevel < MinimumAccessLevelForThresholdEdit)
        {
            throw new AuthorizationException(LocalizedText.Format("Services.Authorization.AccessLevelDenied", MinimumAccessLevelForThresholdEdit));
        }
    }

    private static double ParseThreshold(string? text)
    {
        var normalized = text?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new UserFriendlyException(LocalizedText.Get("ViewModels.WearPartThresholdVm.ThresholdValueInvalid"));
        }

        if (double.TryParse(normalized, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var invariantValue))
        {
            return invariantValue;
        }

        if (double.TryParse(normalized, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.CurrentCulture, out var currentCultureValue))
        {
            return currentCultureValue;
        }

        throw new UserFriendlyException(LocalizedText.Get("ViewModels.WearPartThresholdVm.ThresholdValueInvalid"));
    }

    private void ApplyProfile(WearPartThresholdProfile profile)
    {
        ResourceNumber = profile.ResourceNumber;
        PartName = profile.PartName;
        LifetimeType = profile.LifetimeType;
        WarningLifetimeThreshold = FormatThreshold(profile.WarningLifetimeThreshold);
        ShutdownLifetimeThreshold = FormatThreshold(profile.ShutdownLifetimeThreshold);
    }

    private void ApplyPlcSnapshot(WearPartThresholdPlcSnapshot snapshot)
    {
        PlcWarningLifetimeThreshold = FormatThreshold(snapshot.WarningLifetimeThreshold);
        PlcShutdownLifetimeThreshold = FormatThreshold(snapshot.ShutdownLifetimeThreshold);
    }

    private static string FormatThreshold(double value)
    {
        return value.ToString("0.##", CultureInfo.InvariantCulture);
    }

    private bool CanSave()
    {
        return !IsBusy
            && _wearPartDefinitionId != Guid.Empty
            && !string.IsNullOrWhiteSpace(WarningLifetimeThreshold)
            && !string.IsNullOrWhiteSpace(ShutdownLifetimeThreshold)
            && _currentUserAccessor.CurrentUser?.AccessLevel >= MinimumAccessLevelForThresholdEdit;
    }

    private bool CanRefreshPlc()
    {
        return !IsBusy && _wearPartDefinitionId != Guid.Empty;
    }

    private bool CanOverwritePlc()
    {
        return !IsBusy
            && _wearPartDefinitionId != Guid.Empty
            && !string.IsNullOrWhiteSpace(WarningLifetimeThreshold)
            && !string.IsNullOrWhiteSpace(ShutdownLifetimeThreshold)
            && _currentUserAccessor.CurrentUser?.AccessLevel >= MinimumAccessLevelForThresholdEdit;
    }

    private void Cancel()
    {
        RequestClose?.Invoke(this, false);
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