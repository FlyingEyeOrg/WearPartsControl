using System.Windows;
using WearPartsControl.ViewModels;

namespace WearPartsControl.Views;

/// <summary>
/// AddPartWindow.xaml 的交互逻辑
/// </summary>
public partial class AddPartWindow : Window
{
    public AddPartWindow(AddPartWindowViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();

        Closed += OnClosed;
        viewModel.RequestClose += OnRequestClose;
    }

    public AddPartWindowViewModel ViewModel { get; }

    private void OnClosed(object? sender, EventArgs e)
    {
        Closed -= OnClosed;
        ViewModel.RequestClose -= OnRequestClose;
    }

    private void OnRequestClose(object? sender, bool? dialogResult)
    {
        DialogResult = dialogResult;
        Close();
    }
}
