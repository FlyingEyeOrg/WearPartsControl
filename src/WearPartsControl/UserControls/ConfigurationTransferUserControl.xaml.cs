using System.Windows;
using System.Windows.Controls;
using WearPartsControl.ApplicationServices.Dialogs;
using WearPartsControl.ApplicationServices.Localization;
using WearPartsControl.ViewModels;

namespace WearPartsControl.UserControls;

public partial class ConfigurationTransferUserControl : UserControl
{
    private readonly ConfigurationTransferViewModel _viewModel;
    private readonly IFileDialogService _fileDialogService;
    private readonly IAppDialogService _dialogService;
    private bool _isInitialized;

    public ConfigurationTransferUserControl(
        ConfigurationTransferViewModel viewModel,
        IFileDialogService fileDialogService,
        IAppDialogService dialogService)
    {
        _viewModel = viewModel;
        _fileDialogService = fileDialogService;
        _dialogService = dialogService;
        DataContext = viewModel;
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        _viewModel.ExportRequested += OnExportRequested;
        _viewModel.ImportRequested += OnImportRequested;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_isInitialized)
        {
            return;
        }

        _isInitialized = true;
        await _viewModel.InitializeAsync().ConfigureAwait(true);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        Unloaded -= OnUnloaded;
        _viewModel.ExportRequested -= OnExportRequested;
        _viewModel.ImportRequested -= OnImportRequested;
    }

    private async void OnExportRequested(object? sender, EventArgs e)
    {
        var packagePath = _fileDialogService.ShowSaveFileDialog(
            new SaveFileDialogRequest(
                $"WearPartsControl-Config-{DateTime.Now:yyyyMMddHHmmss}.cfg",
                LocalizedText.Get("Dialogs.ConfigPackageFilter"),
                ".cfg"),
            Window.GetWindow(this));

        if (string.IsNullOrWhiteSpace(packagePath))
        {
            _viewModel.NotifyExportCanceled();
            return;
        }

        try
        {
            await _viewModel.ExportAsync(packagePath).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _viewModel.NotifyTransferFailed(ex.Message);
            _dialogService.ShowMessage(ex.Message, LocalizedText.Get("FriendlyErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Warning, Window.GetWindow(this));
        }
    }

    private async void OnImportRequested(object? sender, EventArgs e)
    {
        var packagePath = _fileDialogService.ShowOpenFileDialog(
            new OpenFileDialogRequest(
                LocalizedText.Get("ConfigurationTransferControl.ImportDialogTitle"),
                LocalizedText.Get("Dialogs.ConfigPackageFilter")),
            Window.GetWindow(this));

        if (string.IsNullOrWhiteSpace(packagePath))
        {
            _viewModel.NotifyImportCanceled();
            return;
        }

        var owner = Window.GetWindow(this);
        var confirmResult = _dialogService.ShowMessage(
            LocalizedText.Get("ConfigurationTransferControl.ImportConfirmMessage"),
            LocalizedText.Get("ConfigurationTransferControl.ImportConfirmTitle"),
            MessageBoxButton.YesNo,
            MessageBoxImage.Question,
            owner,
            MessageBoxResult.No);

        if (confirmResult != MessageBoxResult.Yes)
        {
            _viewModel.NotifyImportCanceled();
            return;
        }

        try
        {
            await _viewModel.ImportAsync(packagePath).ConfigureAwait(true);
            _dialogService.ShowMessage(
                LocalizedText.Get("ConfigurationTransferControl.ImportCompletedMessage"),
                LocalizedText.Get("ConfigurationTransferControl.ImportCompletedTitle"),
                MessageBoxButton.OK,
                MessageBoxImage.Information,
                owner);
        }
        catch (Exception ex)
        {
            _viewModel.NotifyTransferFailed(ex.Message);
            _dialogService.ShowMessage(ex.Message, LocalizedText.Get("FriendlyErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Warning, owner);
        }
    }
}