using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WearPartsControl.ApplicationServices;
using WearPartsControl.ApplicationServices.AppSettings;
using WearPartsControl.ApplicationServices.ConfigurationTransfer;
using WearPartsControl.ApplicationServices.Localization;
using WearPartsControl.Exceptions;

namespace WearPartsControl.ViewModels;

public sealed class ConfigurationTransferViewModel : LocalizedViewModelBase
{
    private readonly IConfigurationTransferService _configurationTransferService;
    private readonly IAppSettingsService _appSettingsService;
    private readonly IUiDispatcher _uiDispatcher;
    private readonly IUiBusyService _uiBusyService;
    private bool _isBusy;
    private bool _canImport;
    private string _statusMessage = LocalizedText.Get("ViewModels.ConfigurationTransferVm.PromptExportOrImport");
    private string _importAvailabilityMessage = LocalizedText.Get("ViewModels.ConfigurationTransferVm.ImportAvailabilityChecking");
    private Func<string>? _statusMessageFactory;
    private Func<string>? _importAvailabilityMessageFactory;

    public ConfigurationTransferViewModel(
        IConfigurationTransferService configurationTransferService,
        IAppSettingsService appSettingsService,
        IUiDispatcher uiDispatcher,
        IUiBusyService uiBusyService)
    {
        _configurationTransferService = configurationTransferService;
        _appSettingsService = appSettingsService;
        _uiDispatcher = uiDispatcher;
        _uiBusyService = uiBusyService;
        ExportCommand = new RelayCommand(RequestExport, CanTransfer);
        ImportCommand = new RelayCommand(RequestImport, CanImportConfiguration);
        SetLocalizedStatusMessage(() => LocalizedText.Get("ViewModels.ConfigurationTransferVm.PromptExportOrImport"));
        SetLocalizedImportAvailabilityMessage(() => LocalizedText.Get("ViewModels.ConfigurationTransferVm.ImportAvailabilityChecking"));
    }

    public event EventHandler? ExportRequested;

    public event EventHandler? ImportRequested;

    public IRelayCommand ExportCommand { get; }

    public IRelayCommand ImportCommand { get; }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(IsNotBusy));
                ExportCommand.NotifyCanExecuteChanged();
                ImportCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool IsNotBusy => !IsBusy;

    public bool CanImport
    {
        get => _canImport;
        private set
        {
            if (SetProperty(ref _canImport, value))
            {
                ImportCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string ImportAvailabilityMessage
    {
        get => _importAvailabilityMessage;
        private set => SetProperty(ref _importAvailabilityMessage, value);
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await RefreshImportAvailabilityAsync(cancellationToken).ConfigureAwait(true);
    }

    public async Task ExportAsync(string packagePath, CancellationToken cancellationToken = default)
    {
        await RunTransferAsync(
            () => LocalizedText.Get("ViewModels.ConfigurationTransferVm.Exporting"),
            async () =>
            {
                var summary = await _configurationTransferService.ExportAsync(packagePath, cancellationToken).ConfigureAwait(true);
                SetLocalizedStatusMessage(() => LocalizedText.Format("ViewModels.ConfigurationTransferVm.ExportSucceeded", summary.PackagePath, summary.FileCount));
            }).ConfigureAwait(true);
    }

    public async Task ImportAsync(string packagePath, CancellationToken cancellationToken = default)
    {
        await RefreshImportAvailabilityAsync(cancellationToken).ConfigureAwait(true);
        await RunTransferAsync(
            () => LocalizedText.Get("ViewModels.ConfigurationTransferVm.Importing"),
            async () =>
            {
                var summary = await _configurationTransferService.ImportAsync(packagePath, cancellationToken).ConfigureAwait(true);
                await RefreshImportAvailabilityAsync(cancellationToken).ConfigureAwait(true);
                SetLocalizedStatusMessage(() => LocalizedText.Format("ViewModels.ConfigurationTransferVm.ImportSucceeded", summary.PackagePath, summary.FileCount));
            }).ConfigureAwait(true);
    }

    public void NotifyExportCanceled()
    {
        SetLocalizedStatusMessage(() => LocalizedText.Get("ViewModels.ConfigurationTransferVm.ExportCanceled"));
    }

    public void NotifyImportCanceled()
    {
        SetLocalizedStatusMessage(() => LocalizedText.Get("ViewModels.ConfigurationTransferVm.ImportCanceled"));
    }

    public void NotifyTransferFailed(string message)
    {
        SetLocalizedStatusMessage(() => LocalizedText.Format("ViewModels.ConfigurationTransferVm.TransferFailed", message));
    }

    protected override void OnLocalizationRefreshed()
    {
        if (_statusMessageFactory is not null)
        {
            StatusMessage = _statusMessageFactory();
        }

        if (_importAvailabilityMessageFactory is not null)
        {
            ImportAvailabilityMessage = _importAvailabilityMessageFactory();
        }
    }

    private async Task RefreshImportAvailabilityAsync(CancellationToken cancellationToken)
    {
        var settings = await _appSettingsService.GetAsync(cancellationToken).ConfigureAwait(true);
        var hasConfiguredClientAppInfo = settings.IsSetClientAppInfo && !string.IsNullOrWhiteSpace(settings.ResourceNumber);
        CanImport = true;
        SetLocalizedImportAvailabilityMessage(() => hasConfiguredClientAppInfo
            ? LocalizedText.Get("ViewModels.ConfigurationTransferVm.ImportUnavailableConfigured")
            : LocalizedText.Get("ViewModels.ConfigurationTransferVm.ImportAvailable"));
    }

    private async Task RunTransferAsync(Func<string> busyMessageFactory, Func<Task> operation)
    {
        IsBusy = true;
        SetLocalizedStatusMessage(busyMessageFactory);
        using var busyScope = _uiBusyService.Enter(busyMessageFactory());
        await _uiDispatcher.RenderAsync().ConfigureAwait(true);

        try
        {
            await operation().ConfigureAwait(true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void RequestExport()
    {
        ExportRequested?.Invoke(this, EventArgs.Empty);
    }

    private void RequestImport()
    {
        ImportRequested?.Invoke(this, EventArgs.Empty);
    }

    private bool CanTransfer()
    {
        return !IsBusy;
    }

    private bool CanImportConfiguration()
    {
        return !IsBusy && CanImport;
    }

    private void SetLocalizedStatusMessage(Func<string> factory)
    {
        _statusMessageFactory = factory;
        StatusMessage = factory();
    }

    private void SetLocalizedImportAvailabilityMessage(Func<string> factory)
    {
        _importAvailabilityMessageFactory = factory;
        ImportAvailabilityMessage = factory();
    }
}