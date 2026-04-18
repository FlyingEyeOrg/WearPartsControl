using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using WearPartsControl.ViewModels;

namespace WearPartsControl.Views
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly MainWindowViewModel _viewModel;
        private readonly IServiceProvider _serviceProvider;
        private CancellationTokenSource? _startupCancellationTokenSource;

        public MainWindow(MainWindowViewModel viewModel, IServiceProvider serviceProvider)
        {
            _viewModel = viewModel;
            _serviceProvider = serviceProvider;
            DataContext = viewModel;
            InitializeComponent();

            Loaded += OnMainWindowLoaded;
            _viewModel.LoginRequested += OnLoginRequested;
            Closed += OnMainWindowClosed;
        }

        private async void OnMainWindowLoaded(object sender, RoutedEventArgs e)
        {
            Loaded -= OnMainWindowLoaded;
            _startupCancellationTokenSource = new CancellationTokenSource();

            try
            {
                await Dispatcher.Yield(DispatcherPriority.Background);
                await _viewModel.InitializeAsync(_startupCancellationTokenSource.Token).ConfigureAwait(true);
            }
            catch (OperationCanceledException)
            {
            }
        }

        private void OnLoginRequested(object? sender, EventArgs e)
        {
            var loginWindow = _serviceProvider.GetRequiredService<LoginWindow>();
            loginWindow.Owner = this;
            loginWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            loginWindow.ShowDialog();
        }

        private void OnMainWindowClosed(object? sender, EventArgs e)
        {
            _startupCancellationTokenSource?.Cancel();
            _startupCancellationTokenSource?.Dispose();
            _startupCancellationTokenSource = null;
            Loaded -= OnMainWindowLoaded;
            _viewModel.LoginRequested -= OnLoginRequested;
            Closed -= OnMainWindowClosed;
        }
    }
}
