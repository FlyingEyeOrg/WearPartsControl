using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WearPartsControl.ApplicationServices;
using WearPartsControl.ApplicationServices.ClientAppInfo;
using WearPartsControl.ApplicationServices.ComNotification;
using WearPartsControl.ApplicationServices.Localization;
using WearPartsControl.ApplicationServices.UserConfig;

namespace WearPartsControl.ViewModels;

public sealed class UserConfigViewModel : LocalizedViewModelBase
{
    private static readonly string[] SupportedLanguageCodes = ["zh-CN", "en-US"];

    private readonly IClientAppInfoService _clientAppInfoService;
    private readonly IUserConfigService _userConfigService;
    private readonly IComNotificationService _comNotificationService;
    private readonly ILocalizationService _localizationService;
    private readonly IUiDispatcher _uiDispatcher;
    private readonly IUiBusyService _uiBusyService;
    private UserConfigSnapshot _originalSnapshot = UserConfigSnapshot.Empty;
    private ObservableCollection<LanguageOption> _languageOptions = new();
    private LanguageOption? _selectedLanguageOption;
    private bool _isBusy;
    private bool _isDirty;
    private bool _isInitialized;
    private bool _isUpdatingState;
    private string _meResponsibleWorkId = string.Empty;
    private string _meResponsibleName = string.Empty;
    private string _prdResponsibleWorkId = string.Empty;
    private string _prdResponsibleName = string.Empty;
    private string _replacementOperatorName = string.Empty;
    private string _comAccessToken = string.Empty;
    private string _comSecret = string.Empty;
    private bool _comNotificationEnabled;
    private string _comPushUrl = string.Empty;
    private string _comDeIpaasKeyAuth = string.Empty;
    private long _comAgentId;
    private long _comGroupTemplateId;
    private long _comWorkTemplateId;
    private string _comUserType = string.Empty;
    private bool _spacerValidationEnabled = true;
    private string _spacerValidationUrl = UserConfig.DefaultSpacerValidationUrl;
    private string _spacerValidationUrlRelease = UserConfig.DefaultSpacerValidationUrlRelease;
    private string _spacerValidationTimeoutMilliseconds = UserConfig.DefaultSpacerValidationTimeoutMilliseconds.ToString(CultureInfo.InvariantCulture);
    private bool _spacerValidationIgnoreServerCertificateErrors = true;
    private string _spacerValidationCodeSeparator = UserConfig.DefaultSpacerValidationCodeSeparator;
    private string _spacerValidationExpectedSegmentCount = UserConfig.DefaultSpacerValidationExpectedSegmentCount.ToString(CultureInfo.InvariantCulture);
    private string _selectedLanguage = "zh-CN";
    private string _statusMessage = LocalizedText.Get("ViewModels.UserConfigVm.PromptMaintain");
    private Func<string>? _statusMessageFactory;

    public UserConfigViewModel(IClientAppInfoService clientAppInfoService, IUserConfigService userConfigService, IComNotificationService comNotificationService, ILocalizationService localizationService, IUiDispatcher uiDispatcher, IUiBusyService uiBusyService)
    {
        _clientAppInfoService = clientAppInfoService;
        _userConfigService = userConfigService;
        _comNotificationService = comNotificationService;
        _localizationService = localizationService;
        _uiDispatcher = uiDispatcher;
        _uiBusyService = uiBusyService;
        SaveCommand = new AsyncRelayCommand(SaveAsync, CanSave);
        TestComNotificationCommand = new AsyncRelayCommand(TestComNotificationAsync, CanTestComNotification);
        _selectedLanguage = _localizationService.CurrentCulture.Name;
        RefreshLanguageOptions();
        SetLocalizedStatusMessage(() => LocalizedText.Get("ViewModels.UserConfigVm.PromptMaintain"));
    }

    public ObservableCollection<LanguageOption> LanguageOptions
    {
        get => _languageOptions;
        private set => SetProperty(ref _languageOptions, value);
    }

    public IAsyncRelayCommand SaveCommand { get; }

    public IAsyncRelayCommand TestComNotificationCommand { get; }

    public async Task<ComNotificationPreview> BuildComNotificationPreviewAsync(CancellationToken cancellationToken = default)
    {
        var clientAppInfo = await _clientAppInfoService.GetAsync(cancellationToken).ConfigureAwait(false);
        return ComNotificationMessageFactory.CreateTestPreview(
            clientAppInfo,
            MeResponsibleName,
            MeResponsibleWorkId,
            PrdResponsibleName,
            PrdResponsibleWorkId,
            ReplacementOperatorName);
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

    public string SelectedLanguage
    {
        get => _selectedLanguage;
        set
        {
            if (SetProperty(ref _selectedLanguage, value))
            {
                SyncSelectedLanguageOption();
                UpdateDirtyState();
            }
        }
    }

    public LanguageOption? SelectedLanguageOption
    {
        get => _selectedLanguageOption;
        set
        {
            if (!SetProperty(ref _selectedLanguageOption, value))
            {
                return;
            }

            if (_isUpdatingState)
            {
                return;
            }

            if (value is null)
            {
                RestoreSelectedLanguageOption();
                return;
            }

            var nextLanguage = value?.Code ?? string.Empty;
            if (SetProperty(ref _selectedLanguage, nextLanguage, nameof(SelectedLanguage)))
            {
                UpdateDirtyState();
            }
        }
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

    public string MeResponsibleName
    {
        get => _meResponsibleName;
        set
        {
            if (SetProperty(ref _meResponsibleName, value))
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

    public string PrdResponsibleName
    {
        get => _prdResponsibleName;
        set
        {
            if (SetProperty(ref _prdResponsibleName, value))
            {
                UpdateDirtyState();
            }
        }
    }

    public string ReplacementOperatorName
    {
        get => _replacementOperatorName;
        set
        {
            if (SetProperty(ref _replacementOperatorName, value))
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

    public bool ComNotificationEnabled
    {
        get => _comNotificationEnabled;
        set
        {
            if (SetProperty(ref _comNotificationEnabled, value))
            {
                UpdateDirtyState();
            }
        }
    }

    public string ComPushUrl
    {
        get => _comPushUrl;
        private set => SetProperty(ref _comPushUrl, value);
    }

    public string ComDeIpaasKeyAuth
    {
        get => _comDeIpaasKeyAuth;
        private set => SetProperty(ref _comDeIpaasKeyAuth, value);
    }

    public long ComAgentId
    {
        get => _comAgentId;
        private set => SetProperty(ref _comAgentId, value);
    }

    public long ComGroupTemplateId
    {
        get => _comGroupTemplateId;
        private set => SetProperty(ref _comGroupTemplateId, value);
    }

    public long ComWorkTemplateId
    {
        get => _comWorkTemplateId;
        private set => SetProperty(ref _comWorkTemplateId, value);
    }

    public string ComUserType
    {
        get => _comUserType;
        private set => SetProperty(ref _comUserType, value);
    }

    public bool SpacerValidationEnabled
    {
        get => _spacerValidationEnabled;
        set
        {
            if (SetProperty(ref _spacerValidationEnabled, value))
            {
                UpdateDirtyState();
            }
        }
    }

    public string SpacerValidationUrl
    {
        get => _spacerValidationUrl;
        set
        {
            if (SetProperty(ref _spacerValidationUrl, value))
            {
                UpdateDirtyState();
            }
        }
    }

    public string SpacerValidationTimeoutMilliseconds
    {
        get => _spacerValidationTimeoutMilliseconds;
        set
        {
            if (SetProperty(ref _spacerValidationTimeoutMilliseconds, value))
            {
                UpdateDirtyState();
            }
        }
    }

    public bool SpacerValidationIgnoreServerCertificateErrors
    {
        get => _spacerValidationIgnoreServerCertificateErrors;
        set
        {
            if (SetProperty(ref _spacerValidationIgnoreServerCertificateErrors, value))
            {
                UpdateDirtyState();
            }
        }
    }

    public string SpacerValidationCodeSeparator
    {
        get => _spacerValidationCodeSeparator;
        set
        {
            if (SetProperty(ref _spacerValidationCodeSeparator, value))
            {
                UpdateDirtyState();
            }
        }
    }

    public string SpacerValidationExpectedSegmentCount
    {
        get => _spacerValidationExpectedSegmentCount;
        set
        {
            if (SetProperty(ref _spacerValidationExpectedSegmentCount, value))
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
            await _uiDispatcher.RunAsync(() =>
            {
                ApplyConfig(config);
                _originalSnapshot = CaptureSnapshot();
                IsDirty = false;
                _isInitialized = true;
                SetLocalizedStatusMessage(() => LocalizedText.Get("ViewModels.UserConfigVm.Loaded"));
            }).ConfigureAwait(false);
        }, LocalizedText.Get("ViewModels.UserConfigVm.LoadFailedPrefix"), LocalizedText.Get("ViewModels.UserConfigVm.PromptMaintain"), cancellationToken).ConfigureAwait(false);
    }

    private bool CanSave() => IsDirty && !IsBusy;

    private bool CanTestComNotification() => !IsBusy;

    private async Task SaveAsync()
    {
        await ExecuteBusyAsync(async () =>
        {
            var config = BuildConfig();
            await _userConfigService.SaveAsync(config).ConfigureAwait(false);
            await _localizationService.SetCultureAsync(SelectedLanguage).ConfigureAwait(false);
            await _uiDispatcher.RunAsync(() =>
            {
                RefreshLanguageOptions();
                _originalSnapshot = CaptureSnapshot();
                IsDirty = false;
                SetLocalizedStatusMessage(() => LocalizedText.Get("ViewModels.UserConfigVm.Saved"));
                _isInitialized = true;
            }).ConfigureAwait(false);
        }, LocalizedText.Get("ViewModels.UserConfigVm.SaveFailedPrefix"), LocalizedText.Get("ViewModels.UserConfigVm.PromptMaintain")).ConfigureAwait(false);
    }

    private async Task TestComNotificationAsync()
    {
        await ExecuteBusyAsync(async () =>
        {
            if (IsDirty)
            {
                await _userConfigService.SaveAsync(BuildConfig()).ConfigureAwait(false);
                await _uiDispatcher.RunAsync(() =>
                {
                    _originalSnapshot = CaptureSnapshot();
                    IsDirty = false;
                }).ConfigureAwait(false);
            }

            var recipients = ResolveRecipients();
            if (recipients.Length == 0)
            {
                throw new InvalidOperationException(LocalizedText.Get("ViewModels.UserConfigVm.ResponsibleMissing"));
            }

            var clientAppInfo = await _clientAppInfoService.GetAsync().ConfigureAwait(false);
            var message = ComNotificationMessageFactory.CreateTestMessage(
                clientAppInfo,
                MeResponsibleName,
                MeResponsibleWorkId,
                PrdResponsibleName,
                PrdResponsibleWorkId,
                ReplacementOperatorName);

            await _comNotificationService.NotifyGroupAsync(
                message.Title,
                message.Markdown,
                recipients).ConfigureAwait(false);

            await _comNotificationService.NotifyWorkAsync(
                message.Title,
                message.Markdown,
                recipients).ConfigureAwait(false);

            await _uiDispatcher.RunAsync(() => SetLocalizedStatusMessage(() => LocalizedText.Get("ViewModels.UserConfigVm.TestSucceeded"))).ConfigureAwait(false);
        }, LocalizedText.Get("ViewModels.UserConfigVm.TestFailedPrefix"), LocalizedText.Get("ViewModels.UserConfigVm.PromptMaintain")).ConfigureAwait(false);
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

    private void RefreshLanguageOptions()
    {
        UpdateLanguageOptionDisplayName("zh-CN", LocalizedText.Get("ViewModels.UserConfigVm.LanguageZhCn"));
        UpdateLanguageOptionDisplayName("en-US", LocalizedText.Get("ViewModels.UserConfigVm.LanguageEnUs"));
        RemoveUnsupportedLanguageOptions();

        SyncSelectedLanguageOption();
    }

    private void UpdateLanguageOptionDisplayName(string code, string displayName)
    {
        var option = LanguageOptions.FirstOrDefault(existing => string.Equals(existing.Code, code, StringComparison.Ordinal));

        if (option is null)
        {
            LanguageOptions.Add(new LanguageOption(code, displayName));
            return;
        }

        option.DisplayName = displayName;
    }

    private void RemoveUnsupportedLanguageOptions()
    {
        for (var index = LanguageOptions.Count - 1; index >= 0; index--)
        {
            if (SupportedLanguageCodes.Contains(LanguageOptions[index].Code, StringComparer.Ordinal))
            {
                continue;
            }

            LanguageOptions.RemoveAt(index);
        }
    }

    private void SyncSelectedLanguageOption()
    {
        var selectedOption = LanguageOptions.FirstOrDefault(option => string.Equals(option.Code, _selectedLanguage, StringComparison.Ordinal));

        _isUpdatingState = true;
        try
        {
            SelectedLanguageOption = selectedOption;
        }
        finally
        {
            _isUpdatingState = false;
        }
    }

    private void RestoreSelectedLanguageOption()
    {
        var selectedOption = LanguageOptions.FirstOrDefault(option => string.Equals(option.Code, _selectedLanguage, StringComparison.Ordinal));
        if (selectedOption is null)
        {
            return;
        }

        _isUpdatingState = true;
        try
        {
            SelectedLanguageOption = selectedOption;
        }
        finally
        {
            _isUpdatingState = false;
        }
    }

    private UserConfig BuildConfig()
    {
        if (!int.TryParse(SpacerValidationTimeoutMilliseconds?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var timeoutMilliseconds)
            || timeoutMilliseconds <= 0)
        {
            throw new InvalidOperationException(LocalizedText.Get("ViewModels.UserConfigVm.SpacerValidationTimeoutInvalid"));
        }

        if (!int.TryParse(SpacerValidationExpectedSegmentCount?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var expectedSegmentCount)
            || expectedSegmentCount <= 0)
        {
            throw new InvalidOperationException(LocalizedText.Get("ViewModels.UserConfigVm.SpacerValidationExpectedSegmentCountInvalid"));
        }

        return new UserConfig
        {
            MeResponsibleWorkId = MeResponsibleWorkId,
            MeResponsibleName = MeResponsibleName,
            PrdResponsibleWorkId = PrdResponsibleWorkId,
            PrdResponsibleName = PrdResponsibleName,
            ReplacementOperatorName = ReplacementOperatorName,
            Language = SelectedLanguage,
            ComAccessToken = ComAccessToken,
            ComSecret = ComSecret,
            ComNotificationEnabled = ComNotificationEnabled,
            ComPushUrl = ComPushUrl,
            ComDeIpaasKeyAuth = ComDeIpaasKeyAuth,
            ComAgentId = ComAgentId,
            ComGroupTemplateId = ComGroupTemplateId,
            ComWorkTemplateId = ComWorkTemplateId,
            ComUserType = ComUserType,
            ComTimeoutMilliseconds = UserConfig.DefaultComTimeoutMilliseconds,
            SpacerValidationEnabled = SpacerValidationEnabled,
            SpacerValidationUrl = SpacerValidationUrl,
            SpacerValidationUrlRelease = _spacerValidationUrlRelease,
            SpacerValidationTimeoutMilliseconds = timeoutMilliseconds,
            SpacerValidationIgnoreServerCertificateErrors = SpacerValidationIgnoreServerCertificateErrors,
            SpacerValidationCodeSeparator = SpacerValidationCodeSeparator,
            SpacerValidationExpectedSegmentCount = expectedSegmentCount
        };
    }

    private void ApplyConfig(UserConfig config)
    {
        _isUpdatingState = true;
        try
        {
            MeResponsibleWorkId = config.MeResponsibleWorkId;
            MeResponsibleName = config.MeResponsibleName;
            PrdResponsibleWorkId = config.PrdResponsibleWorkId;
            PrdResponsibleName = config.PrdResponsibleName;
            ReplacementOperatorName = config.ReplacementOperatorName;
            ComAccessToken = config.ComAccessToken;
            ComSecret = config.ComSecret;
            ComNotificationEnabled = config.ComNotificationEnabled;
            ComPushUrl = config.ComPushUrl;
            ComDeIpaasKeyAuth = config.ComDeIpaasKeyAuth;
            ComAgentId = config.ComAgentId;
            ComGroupTemplateId = config.ComGroupTemplateId;
            ComWorkTemplateId = config.ComWorkTemplateId;
            ComUserType = config.ComUserType;
            SpacerValidationEnabled = config.SpacerValidationEnabled;
            SpacerValidationUrl = config.SpacerValidationUrl;
            _spacerValidationUrlRelease = config.SpacerValidationUrlRelease;
            SpacerValidationTimeoutMilliseconds = config.SpacerValidationTimeoutMilliseconds.ToString(CultureInfo.InvariantCulture);
            SpacerValidationIgnoreServerCertificateErrors = config.SpacerValidationIgnoreServerCertificateErrors;
            SpacerValidationCodeSeparator = config.SpacerValidationCodeSeparator;
            SpacerValidationExpectedSegmentCount = config.SpacerValidationExpectedSegmentCount.ToString(CultureInfo.InvariantCulture);
            SelectedLanguage = string.IsNullOrWhiteSpace(config.Language)
                ? _localizationService.CurrentCulture.Name
                : config.Language;
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
            MeResponsibleName?.Trim() ?? string.Empty,
            PrdResponsibleWorkId?.Trim() ?? string.Empty,
            PrdResponsibleName?.Trim() ?? string.Empty,
            ReplacementOperatorName?.Trim() ?? string.Empty,
            ComAccessToken?.Trim() ?? string.Empty,
            ComSecret?.Trim() ?? string.Empty,
            ComNotificationEnabled,
            ComPushUrl?.Trim() ?? string.Empty,
            ComDeIpaasKeyAuth?.Trim() ?? string.Empty,
            ComAgentId,
            ComGroupTemplateId,
            ComWorkTemplateId,
            ComUserType?.Trim() ?? string.Empty,
            SpacerValidationEnabled,
            SpacerValidationUrl?.Trim() ?? string.Empty,
            SpacerValidationTimeoutMilliseconds?.Trim() ?? string.Empty,
            SpacerValidationIgnoreServerCertificateErrors,
            SpacerValidationCodeSeparator?.Trim() ?? string.Empty,
            SpacerValidationExpectedSegmentCount?.Trim() ?? string.Empty,
            SelectedLanguage?.Trim() ?? string.Empty);
    }

    private async Task ExecuteBusyAsync(Func<Task> action, string errorPrefix, string busyMessage, CancellationToken cancellationToken = default)
    {
        try
        {
            await _uiDispatcher.RunAsync(() => IsBusy = true).ConfigureAwait(false);
            using var _ = _uiBusyService.Enter(busyMessage);
            await _uiDispatcher.RenderAsync().ConfigureAwait(false);
            await action().ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            await _uiDispatcher.RunAsync(() => SetRawStatusMessage(errorPrefix + ex.Message)).ConfigureAwait(false);
        }
        finally
        {
            await _uiDispatcher.RunAsync(() => IsBusy = false).ConfigureAwait(false);
        }
    }

    protected override void OnLocalizationRefreshed()
    {
        RefreshLanguageOptions();

        if (_statusMessageFactory is not null)
        {
            StatusMessage = _statusMessageFactory();
        }
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

    public sealed class LanguageOption : ObservableObject
    {
        private string _displayName;

        public LanguageOption(string code, string displayName)
        {
            Code = code;
            _displayName = displayName;
        }

        public string Code { get; }

        public string DisplayName
        {
            get => _displayName;
            set => SetProperty(ref _displayName, value);
        }
    }

    private sealed record UserConfigSnapshot(
        string MeResponsibleWorkId,
        string MeResponsibleName,
        string PrdResponsibleWorkId,
        string PrdResponsibleName,
        string ReplacementOperatorName,
        string ComAccessToken,
        string ComSecret,
        bool ComNotificationEnabled,
        string ComPushUrl,
        string ComDeIpaasKeyAuth,
        long ComAgentId,
        long ComGroupTemplateId,
        long ComWorkTemplateId,
        string ComUserType,
        bool SpacerValidationEnabled,
        string SpacerValidationUrl,
        string SpacerValidationTimeoutMilliseconds,
        bool SpacerValidationIgnoreServerCertificateErrors,
        string SpacerValidationCodeSeparator,
        string SpacerValidationExpectedSegmentCount,
        string SelectedLanguage)
    {
        public static UserConfigSnapshot Empty { get; } = new(
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            UserConfig.DefaultComNotificationEnabled,
            UserConfig.DefaultComPushUrl,
            UserConfig.DefaultComDeIpaasKeyAuth,
            UserConfig.DefaultComAgentId,
            UserConfig.DefaultComGroupTemplateId,
            UserConfig.DefaultComWorkTemplateId,
            UserConfig.DefaultComUserType,
            true,
            UserConfig.DefaultSpacerValidationUrl,
            UserConfig.DefaultSpacerValidationTimeoutMilliseconds.ToString(CultureInfo.InvariantCulture),
            true,
            UserConfig.DefaultSpacerValidationCodeSeparator,
            UserConfig.DefaultSpacerValidationExpectedSegmentCount.ToString(CultureInfo.InvariantCulture),
            "zh-CN");
    }
}