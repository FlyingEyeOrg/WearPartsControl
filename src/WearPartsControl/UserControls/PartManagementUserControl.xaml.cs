using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
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

    public PartManagementUserControl(PartManagementViewModel viewModel, IServiceProvider serviceProvider)
    {
        _viewModel = viewModel;
        _serviceProvider = serviceProvider;
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

        if (dialog.ShowDialog() == true)
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

        if (dialog.ShowDialog() == true)
        {
            await _viewModel.RefreshAsync();
        }
    }
}
