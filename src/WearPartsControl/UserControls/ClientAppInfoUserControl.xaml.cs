using System.Windows.Controls;
using Microsoft.Win32;
using WearPartsControl.ApplicationServices.Localization;
using WearPartsControl.ViewModels;

namespace WearPartsControl.UserControls
{
    /// <summary>
    /// ClientAppInfoUserControl.xaml 的交互逻辑
    /// </summary>
    public partial class ClientAppInfoUserControl : UserControl
    {
        private readonly ClientAppInfoViewModel _viewModel;
        private bool _isInitialized;

        public ClientAppInfoUserControl(ClientAppInfoViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
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
                Filter = "SQLite 数据库|*.db;*.sqlite;*.sqlite3|所有文件|*.*",
                CheckFileExists = true,
                Multiselect = false
            };

            if (dialog.ShowDialog() != true)
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
                System.Windows.MessageBox.Show(ex.Message, LocalizedText.Get("FriendlyErrorTitle"), System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            }
        }
    }
}
