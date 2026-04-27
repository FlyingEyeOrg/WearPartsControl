using System.Windows.Controls;
using System.Text;
using System.IO;
using System.Threading;
using WearPartsControl.ApplicationServices.Dialogs;
using WearPartsControl.ApplicationServices.Localization;
using WearPartsControl.ApplicationServices.LoginService;
using WearPartsControl.ViewModels;

namespace WearPartsControl.UserControls;

/// <summary>
/// PartReplacementHistoryUserControl.xaml 的交互逻辑
/// </summary>
public partial class PartReplacementHistoryUserControl : UserControl
{
    private readonly PartReplacementHistoryViewModel _viewModel;
    private readonly IAutoLogoutInteractionService _autoLogoutInteractionService;
    private readonly IFileDialogService _fileDialogService;
    private bool _isInitialized;

    public PartReplacementHistoryUserControl(PartReplacementHistoryViewModel viewModel, IAutoLogoutInteractionService autoLogoutInteractionService, IFileDialogService? fileDialogService = null)
    {
        _viewModel = viewModel;
        _autoLogoutInteractionService = autoLogoutInteractionService;
        _fileDialogService = fileDialogService ?? new FileDialogService(autoLogoutInteractionService);
        DataContext = viewModel;
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        _viewModel.ExportRequested += OnExportRequested;
    }

    private async void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (_isInitialized)
        {
            await _viewModel.RefreshAsync(CancellationToken.None).ConfigureAwait(true);
            return;
        }

        _isInitialized = true;
        await _viewModel.InitializeAsync().ConfigureAwait(true);
    }

    private void OnUnloaded(object sender, System.Windows.RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        Unloaded -= OnUnloaded;
        _viewModel.ExportRequested -= OnExportRequested;
    }

    private async void OnExportRequested(object? sender, PartReplacementHistoryExportRequestedEventArgs e)
    {
        try
        {
            var fileName = _fileDialogService.ShowSaveFileDialog(
                new SaveFileDialogRequest(
                    e.SuggestedFileName,
                    LocalizedText.Get("Dialogs.CsvFileFilter"),
                    ".csv"),
                System.Windows.Window.GetWindow(this));

            if (string.IsNullOrWhiteSpace(fileName))
            {
                _viewModel.NotifyExportCanceled();
                return;
            }

            await System.IO.File.WriteAllTextAsync(fileName, e.Content, new UTF8Encoding(true)).ConfigureAwait(true);
            _viewModel.NotifyExportSucceeded(fileName);
        }
        catch (Exception ex)
        {
            _viewModel.NotifyExportFailed(ex.Message);
        }
    }
}
