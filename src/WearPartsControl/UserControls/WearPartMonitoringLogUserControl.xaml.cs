using System.Collections.Specialized;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using WearPartsControl.ApplicationServices.Dialogs;
using WearPartsControl.ApplicationServices.Localization;
using WearPartsControl.ViewModels;

namespace WearPartsControl.UserControls;

public partial class WearPartMonitoringLogUserControl : UserControl
{
    private readonly WearPartMonitoringLogViewModel _viewModel;
    private readonly IFileDialogService _fileDialogService;

    public WearPartMonitoringLogUserControl(WearPartMonitoringLogViewModel viewModel, IFileDialogService fileDialogService)
    {
        _viewModel = viewModel;
        _fileDialogService = fileDialogService;
        DataContext = viewModel;
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        _viewModel.Entries.CollectionChanged += OnEntriesCollectionChanged;
        _viewModel.CopyRequested += OnCopyRequested;
        _viewModel.ExportRequested += OnExportRequested;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ScrollToLatestEntry();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        Unloaded -= OnUnloaded;
        _viewModel.Entries.CollectionChanged -= OnEntriesCollectionChanged;
        _viewModel.CopyRequested -= OnCopyRequested;
        _viewModel.ExportRequested -= OnExportRequested;
        _viewModel.Dispose();
    }

    private void OnEntriesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (!_viewModel.IsAutoScrollEnabled || _viewModel.Entries.Count == 0)
        {
            return;
        }

        Dispatcher.InvokeAsync(ScrollToLatestEntry, DispatcherPriority.Background);
    }

    private void ScrollToLatestEntry()
    {
        if (!_viewModel.IsAutoScrollEnabled || _viewModel.Entries.Count == 0)
        {
            return;
        }

        LogDataGrid.ScrollIntoView(_viewModel.Entries[^1]);
    }

    private void OnCopyRequested(object? sender, WearPartMonitoringLogCopyRequestedEventArgs e)
    {
        try
        {
            Clipboard.SetText(e.Content);
            _viewModel.NotifyCopySucceeded();
        }
        catch (Exception ex)
        {
            _viewModel.NotifyCopyFailed(ex.Message);
        }
    }

    private async void OnExportRequested(object? sender, WearPartMonitoringLogExportRequestedEventArgs e)
    {
        try
        {
            var fileName = _fileDialogService.ShowSaveFileDialog(
                new SaveFileDialogRequest(
                    e.SuggestedFileName,
                    LocalizedText.Get("Dialogs.CsvFileFilter"),
                    ".csv"),
                Window.GetWindow(this));

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