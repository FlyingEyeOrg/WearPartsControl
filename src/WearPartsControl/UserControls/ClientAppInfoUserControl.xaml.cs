using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using WearPartsControl.ApplicationServices;
using WearPartsControl.ApplicationServices.AppSettings;
using WearPartsControl.ApplicationServices.Dialogs;
using WearPartsControl.ApplicationServices.Localization;
using WearPartsControl.ApplicationServices.LoginService;
using WearPartsControl.ViewModels;
using WearPartsControl.Views;

namespace WearPartsControl.UserControls
{
    /// <summary>
    /// ClientAppInfoUserControl.xaml 的交互逻辑
    /// </summary>
    public partial class ClientAppInfoUserControl : UserControl
    {
        private readonly ClientAppInfoViewModel _viewModel;
        private readonly IServiceProvider _serviceProvider;
        private readonly IAutoLogoutInteractionService _autoLogoutInteractionService;
        private readonly IAppDialogService _dialogService;
        private readonly ICurrentUserAccessor _currentUserAccessor;
        private readonly IAppSettingsService _appSettingsService;
        private bool _isInitialized;

        public ClientAppInfoUserControl(
            ClientAppInfoViewModel viewModel,
            IServiceProvider serviceProvider,
            IAutoLogoutInteractionService autoLogoutInteractionService,
            ICurrentUserAccessor currentUserAccessor,
            IAppSettingsService appSettingsService,
            IAppDialogService? dialogService = null)
        {
            InitializeComponent();
            _viewModel = viewModel;
            _serviceProvider = serviceProvider;
            _autoLogoutInteractionService = autoLogoutInteractionService;
            _dialogService = dialogService ?? new AppDialogService(autoLogoutInteractionService);
            _currentUserAccessor = currentUserAccessor;
            _appSettingsService = appSettingsService;
            DataContext = viewModel;
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            _viewModel.ImportLegacyConfigurationRequested += OnImportLegacyConfigurationRequested;
        }

        private async void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
        {
            if (_isInitialized)
            {
                return;
            }

            _isInitialized = true;
            await _viewModel.InitializeAsync().ConfigureAwait(true);
        }

        private void OnUnloaded(object sender, System.Windows.RoutedEventArgs e)
        {
            Loaded -= OnLoaded;
            Unloaded -= OnUnloaded;
            _viewModel.ImportLegacyConfigurationRequested -= OnImportLegacyConfigurationRequested;
        }

        private async void OnImportLegacyConfigurationRequested(object? sender, EventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = LocalizedText.Get("ViewModels.ClientAppInfoVm.ImportDialogTitle"),
                Filter = LocalizedText.Get("Dialogs.SQLiteDatabaseFilter"),
                CheckFileExists = true,
                Multiselect = false
            };

            var dialogResult = _autoLogoutInteractionService.RunModal(() => dialog.ShowDialog() == true);
            if (!dialogResult)
            {
                _viewModel.NotifyLegacyConfigurationImportCanceled();
                return;
            }

            try
            {
                await _viewModel.ImportLegacyConfigurationAsync(dialog.FileName).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage(ex.Message, LocalizedText.Get("FriendlyErrorTitle"), System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning, System.Windows.Window.GetWindow(this));
            }
        }

        private async void OnImportLegacyConfigurationClicked(object sender, System.Windows.RoutedEventArgs e)
        {
            if (!_viewModel.IsImportLegacyConfigurationEnabled)
            {
                return;
            }

            if (!await EnsureAuthenticatedForConfiguredClientAsync().ConfigureAwait(true))
            {
                return;
            }

            _viewModel.ImportLegacyConfigurationCommand.Execute(null);
        }

        private async void OnSaveClicked(object sender, System.Windows.RoutedEventArgs e)
        {
            if (!_viewModel.IsSaveClientAppInfoEnabled)
            {
                return;
            }

            if (!await EnsureAuthenticatedForConfiguredClientAsync().ConfigureAwait(true))
            {
                return;
            }

            await _viewModel.SaveCommand.ExecuteAsync(null);
        }

        private async void OnToggleWearPartMonitoringClicked(object sender, System.Windows.RoutedEventArgs e)
        {
            if (!_viewModel.IsToggleWearPartMonitoringEnabled)
            {
                return;
            }

            if (!await EnsureAuthenticatedForConfiguredClientAsync().ConfigureAwait(true))
            {
                return;
            }

            await _viewModel.ToggleWearPartMonitoringCommand.ExecuteAsync(null);
        }

        private async void OnTestPlcConnectionClicked(object sender, System.Windows.RoutedEventArgs e)
        {
            if (!_viewModel.IsTestPlcConnectionEnabled)
            {
                return;
            }

            if (!await EnsureAuthenticatedForConfiguredClientAsync().ConfigureAwait(true))
            {
                return;
            }

            await _viewModel.TestPlcConnectionCommand.ExecuteAsync(null);
        }

        private async Task<bool> EnsureAuthenticatedForConfiguredClientAsync()
        {
            var settings = await _appSettingsService.GetAsync().ConfigureAwait(true);
            if (!settings.IsSetClientAppInfo)
            {
                return true;
            }

            if (_currentUserAccessor.CurrentUser is not null)
            {
                return true;
            }

            var loginWindow = _serviceProvider.GetRequiredService<LoginWindow>();
            var dialogResult = _dialogService.ShowDialog(loginWindow, System.Windows.Window.GetWindow(this));
            return dialogResult && _currentUserAccessor.CurrentUser is not null;
        }
    }
}
