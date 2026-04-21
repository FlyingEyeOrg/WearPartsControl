using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WearPartsControl.ApplicationServices.ComNotification;
using WearPartsControl.ApplicationServices.Localization;
using WearPartsControl.ApplicationServices.UserConfig;

namespace WearPartsControl.ViewModels;

public sealed class UserConfigViewModel : ObservableObject
{
    private readonly IUserConfigService _userConfigService;
    private readonly IComNotificationService _comNotificationService;
    private UserConfigSnapshot _originalSnapshot = UserConfigSnapshot.Empty;
    private bool _isBusy;
    private bool _isDirty;
    private bool _isInitialized;
    private bool _isUpdatingState;
    private string _meResponsibleWorkId = string.Empty;
    private string _prdResponsibleWorkId = string.Empty;
    private string _comAccessToken = string.Empty;
    private string _comSecret = string.Empty;
    private string _statusMessage = LocalizedText.Get("ViewModels.UserConfig.PromptMaintain");

    public UserConfigViewModel(IUserConfigService userConfigService, IComNotificationService comNotificationService)
    {
        _userConfigService = userConfigService;
        _comNotificationService = comNotificationService;
        SaveCommand = new AsyncRelayCommand(SaveAsync, CanSave);
        TestComNotificationCommand = new AsyncRelayCommand(TestComNotificationAsync, CanTestComNotification);
    }

    public IAsyncRelayCommand SaveCommand { get; }

    public IAsyncRelayCommand TestComNotificationCommand { get; }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(IsNotBusy));
                SaveCommand.NotifyCanExecuteChanged();
                TestComNotificationCommand.NotifyCanExecuteChanged();
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

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string MeResponsibleWorkId
    {
        get => _meResponsibleWorkId;
        set
        {
            if (SetProperty(ref _meResponsibleWorkId, value))
            {
                UpdateDirtyState();
            }
        }
    }

    public string PrdResponsibleWorkId
    {
        get => _prdResponsibleWorkId;
        set
        {
            if (SetProperty(ref _prdResponsibleWorkId, value))
            {
                UpdateDirtyState();
            }
        }
    }

    public string ComAccessToken
    {
        get => _comAccessToken;
        set
        {
            if (SetProperty(ref _comAccessToken, value))
            {
                UpdateDirtyState();
            }
        }
    }

    public string ComSecret
    {
        get => _comSecret;
        set
        {
            if (SetProperty(ref _comSecret, value))
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

        await ExecuteBusyAsync(async () =>
        {
            var config = await _userConfigService.GetAsync(cancellationToken).ConfigureAwait(false);
            ApplyConfig(config);
            _originalSnapshot = CaptureSnapshot();
            IsDirty = false;
            _isInitialized = true;
            StatusMessage = LocalizedText.Get("ViewModels.UserConfig.Loaded");
        }, LocalizedText.Get("ViewModels.UserConfig.LoadFailedPrefix"), cancellationToken).ConfigureAwait(false);
    }

    private bool CanSave() => IsDirty && !IsBusy;

    private bool CanTestComNotification() => !IsBusy;

    private async Task SaveAsync()
    {
        await ExecuteBusyAsync(async () =>
        {
            var config = BuildConfig();
            await _userConfigService.SaveAsync(config).ConfigureAwait(false);
            _originalSnapshot = CaptureSnapshot();
            IsDirty = false;
            StatusMessage = LocalizedText.Get("ViewModels.UserConfig.Saved");
            _isInitialized = true;
        }, LocalizedText.Get("ViewModels.UserConfig.SaveFailedPrefix")).ConfigureAwait(false);
    }

    private async Task TestComNotificationAsync()
    {
        await ExecuteBusyAsync(async () =>
        {
            if (IsDirty)
            {
                await _userConfigService.SaveAsync(BuildConfig()).ConfigureAwait(false);
                _originalSnapshot = CaptureSnapshot();
                IsDirty = false;
            }

            var recipients = ResolveRecipients();
            if (recipients.Length == 0)
            {
                throw new InvalidOperationException(LocalizedText.Get("ViewModels.UserConfig.ResponsibleMissing"));
            }

            await _comNotificationService.NotifyGroupAsync(
                LocalizedText.Get("ViewModels.UserConfig.TestNotificationTitle"),
                LocalizedText.Get("ViewModels.UserConfig.TestNotificationBody"),
                recipients).ConfigureAwait(false);

            StatusMessage = LocalizedText.Get("ViewModels.UserConfig.TestSucceeded");
        }, LocalizedText.Get("ViewModels.UserConfig.TestFailedPrefix")).ConfigureAwait(false);
    }

    private string[] ResolveRecipients()
    {
        return new[]
        {
            MeResponsibleWorkId,
            PrdResponsibleWorkId
        }
        .Where(static value => !string.IsNullOrWhiteSpace(value))
        .Select(static value => value.Trim())
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();
    }

    private UserConfig BuildConfig()
    {
        return new UserConfig
        {
            MeResponsibleWorkId = MeResponsibleWorkId,
            PrdResponsibleWorkId = PrdResponsibleWorkId,
            ComAccessToken = ComAccessToken,
            ComSecret = ComSecret
        };
    }

    private void ApplyConfig(UserConfig config)
    {
        _isUpdatingState = true;
        try
        {
            MeResponsibleWorkId = config.MeResponsibleWorkId;
            PrdResponsibleWorkId = config.PrdResponsibleWorkId;
            ComAccessToken = config.ComAccessToken;
            ComSecret = config.ComSecret;
        }
        finally
        {
            _isUpdatingState = false;
        }
    }

    private void UpdateDirtyState()
    {
        if (_isUpdatingState || !_isInitialized)
        {
            return;
        }

        IsDirty = CaptureSnapshot() != _originalSnapshot;
    }

    private UserConfigSnapshot CaptureSnapshot()
    {
        return new UserConfigSnapshot(
            MeResponsibleWorkId?.Trim() ?? string.Empty,
            PrdResponsibleWorkId?.Trim() ?? string.Empty,
            ComAccessToken?.Trim() ?? string.Empty,
            ComSecret?.Trim() ?? string.Empty);
    }

    private async Task ExecuteBusyAsync(Func<Task> action, string errorPrefix, CancellationToken cancellationToken = default)
    {
        try
        {
            IsBusy = true;
            await action().ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            StatusMessage = errorPrefix + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private sealed record UserConfigSnapshot(
        string MeResponsibleWorkId,
        string PrdResponsibleWorkId,
        string ComAccessToken,
        string ComSecret)
    {
        public static UserConfigSnapshot Empty { get; } = new(string.Empty, string.Empty, string.Empty, string.Empty);
    }
}