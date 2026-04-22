using System.Threading;
using System.Windows.Controls;
using WearPartsControl.ViewModels;

namespace WearPartsControl.UserControls;

public partial class ToolChangeManagementUserControl : UserControl
{
    private bool _isInitialized;

    public ToolChangeManagementUserControl(ToolChangeManagementViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is not ToolChangeManagementViewModel viewModel)
        {
            return;
        }

        if (_isInitialized)
        {
            await viewModel.RefreshAsync(CancellationToken.None).ConfigureAwait(true);
            return;
        }

        _isInitialized = true;
        await viewModel.InitializeAsync().ConfigureAwait(true);
    }
}