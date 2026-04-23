using System.Windows.Controls;
using System.Text;
using System.IO;
using System.Threading;
using Microsoft.Win32;
using WearPartsControl.ApplicationServices.Localization;
using WearPartsControl.ApplicationServices.LoginService;
using WearPartsControl.ViewModels;

namespace WearPartsControl.UserControls;

/// <summary>
/// PartUpdateRecordUserControl.xaml 的交互逻辑
/// </summary>
public partial class PartUpdateRecordUserControl : UserControl
{
    private readonly PartUpdateRecordViewModel _viewModel;
    private readonly IAutoLogoutInteractionService _autoLogoutInteractionService;
    private bool _isInitialized;

    public PartUpdateRecordUserControl(PartUpdateRecordViewModel viewModel, IAutoLogoutInteractionService autoLogoutInteractionService)
    {
        _viewModel = viewModel;
        _autoLogoutInteractionService = autoLogoutInteractionService;
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

    private async void OnExportRequested(object? sender, PartUpdateRecordExportRequestedEventArgs e)
    {
        try
        {
            var dialog = new SaveFileDialog
            {
                FileName = e.SuggestedFileName,
                Filter = LocalizedText.Get("Dialogs.CsvFileFilter"),
                DefaultExt = ".csv",
                AddExtension = true,
                OverwritePrompt = true
            };

            var dialogResult = _autoLogoutInteractionService.RunModal(() => dialog.ShowDialog() == true);

            if (!dialogResult)
            {
                _viewModel.NotifyExportCanceled();
                return;
            }

            await System.IO.File.WriteAllTextAsync(dialog.FileName, e.Content, new UTF8Encoding(true)).ConfigureAwait(true);
            _viewModel.NotifyExportSucceeded(dialog.FileName);
        }
        catch (Exception ex)
        {
            _viewModel.NotifyExportFailed(ex.Message);
        }
    }
}
