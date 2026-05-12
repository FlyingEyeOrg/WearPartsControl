using WearPartsControl.ViewModels;

namespace WearPartsControl.Views;

public partial class WearPartThresholdWindow : AppDialogWindow
{
    public WearPartThresholdWindow(WearPartThresholdWindowViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();

        Closed += OnClosed;
        viewModel.RequestClose += OnRequestClose;
    }

    public WearPartThresholdWindowViewModel ViewModel { get; }

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