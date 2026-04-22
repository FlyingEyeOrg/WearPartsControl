using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using WearPartsControl.ApplicationServices.LoginService;
using WearPartsControl.ApplicationServices.PartServices;
using WearPartsControl.ViewModels;
using WearPartsControl.Views;

namespace WearPartsControl.UserControls;

/// <summary>
/// PartManagementUserControl.xaml 的交互逻辑
/// </summary>
public partial class PartManagementUserControl : UserControl
{
    private readonly PartManagementViewModel _viewModel;
    private readonly IServiceProvider _serviceProvider;
    private readonly IAutoLogoutInteractionService _autoLogoutInteractionService;

    public PartManagementUserControl(PartManagementViewModel viewModel, IServiceProvider serviceProvider, IAutoLogoutInteractionService autoLogoutInteractionService)
    {
        _viewModel = viewModel;
        _serviceProvider = serviceProvider;
        _autoLogoutInteractionService = autoLogoutInteractionService;
        DataContext = viewModel;
        InitializeComponent();

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        _viewModel.AddRequested += OnAddRequested;
        _viewModel.EditRequested += OnEditRequested;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await _viewModel.InitializeAsync();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        Unloaded -= OnUnloaded;
        _viewModel.AddRequested -= OnAddRequested;
        _viewModel.EditRequested -= OnEditRequested;
    }

    private async void OnAddRequested(object? sender, EventArgs e)
    {
        var dialog = _serviceProvider.GetRequiredService<AddPartWindow>();
        dialog.Owner = Window.GetWindow(this);
        dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        dialog.ViewModel.InitializeForCreate(_viewModel.ClientAppConfigurationId, _viewModel.ResourceNumber);

        var dialogResult = _autoLogoutInteractionService.RunModal(() => dialog.ShowDialog() == true);

        if (dialogResult)
        {
            await _viewModel.RefreshAsync();
        }
    }

    private async void OnEditRequested(object? sender, WearPartDefinition definition)
    {
        var dialog = _serviceProvider.GetRequiredService<EditPartWindow>();
        dialog.Owner = Window.GetWindow(this);
        dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        dialog.ViewModel.InitializeForEdit(definition);

        var dialogResult = _autoLogoutInteractionService.RunModal(() => dialog.ShowDialog() == true);

        if (dialogResult)
        {
            await _viewModel.RefreshAsync();
        }
    }
}
