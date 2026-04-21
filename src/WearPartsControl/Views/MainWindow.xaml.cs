using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using WearPartsControl.ApplicationServices.Startup;
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
            StartupPerformanceTracker.Mark("主窗口 Loaded 事件触发");

            try
            {
                await Dispatcher.Yield(DispatcherPriority.Background);
                StartupPerformanceTracker.Mark("主窗口让出首帧渲染后继续初始化");
                await _viewModel.InitializeAsync(_startupCancellationTokenSource.Token).ConfigureAwait(true);
                StartupPerformanceTracker.Mark("主窗口视图模型初始化完成");
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
