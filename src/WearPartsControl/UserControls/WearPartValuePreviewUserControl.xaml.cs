using System.Threading;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using WearPartsControl.ApplicationServices.Dialogs;
using WearPartsControl.ApplicationServices.LoginService;
using WearPartsControl.ViewModels;
using WearPartsControl.Views;

namespace WearPartsControl.UserControls;

/// <summary>
/// WearPartValuePreviewUserControl.xaml 的交互逻辑
/// </summary>
public partial class WearPartValuePreviewUserControl : UserControl
{
    private readonly WearPartValuePreviewViewModel _viewModel;
    private readonly IServiceProvider _serviceProvider;
    private readonly IAppDialogService _dialogService;
    private bool _isInitialized;

    public WearPartValuePreviewUserControl(WearPartValuePreviewViewModel viewModel, IServiceProvider serviceProvider, IAutoLogoutInteractionService autoLogoutInteractionService, IAppDialogService? dialogService = null)
    {
        _viewModel = viewModel;
        _serviceProvider = serviceProvider;
        _dialogService = dialogService ?? new AppDialogService(autoLogoutInteractionService);
        InitializeComponent();
        DataContext = viewModel;
        Loaded += OnLoaded;
        _viewModel.ThresholdEditRequested += OnThresholdEditRequested;
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

    private async void OnThresholdEditRequested(object? sender, WearPartValuePreviewRowViewModel row)
    {
        var dialog = _serviceProvider.GetRequiredService<WearPartThresholdWindow>();
        await dialog.ViewModel.InitializeAsync(row.WearPartDefinitionId).ConfigureAwait(true);

        var dialogResult = _dialogService.ShowDialog(dialog, System.Windows.Window.GetWindow(this));
        if (dialogResult)
        {
            await _viewModel.RefreshAsync(CancellationToken.None).ConfigureAwait(true);
        }
    }
}