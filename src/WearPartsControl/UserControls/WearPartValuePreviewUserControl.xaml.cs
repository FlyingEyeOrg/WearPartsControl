using System.Threading;
using System.Windows.Controls;
using System.Windows.Threading;
using WearPartsControl.ViewModels;

namespace WearPartsControl.UserControls;

/// <summary>
/// WearPartValuePreviewUserControl.xaml 的交互逻辑
/// </summary>
public partial class WearPartValuePreviewUserControl : UserControl
{
    private bool _isInitialized;

    public WearPartValuePreviewUserControl(WearPartValuePreviewViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is not WearPartValuePreviewViewModel viewModel)
        {
            return;
        }

        if (_isInitialized)
        {
            await viewModel.RefreshAsync(CancellationToken.None).ConfigureAwait(true);
            return;
        }

        _isInitialized = true;
        await Dispatcher.Yield(DispatcherPriority.Background);
        await viewModel.InitializeAsync().ConfigureAwait(true);
    }
}