using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using WearPartsControl.ApplicationServices.Dialogs;
using WearPartsControl.ApplicationServices.Localization;
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
    private readonly IAppDialogService _dialogService;
    private readonly IFileDialogService _fileDialogService;

    public PartManagementUserControl(PartManagementViewModel viewModel, IServiceProvider serviceProvider, IAutoLogoutInteractionService autoLogoutInteractionService, IAppDialogService? dialogService = null, IFileDialogService? fileDialogService = null)
    {
        _viewModel = viewModel;
        _serviceProvider = serviceProvider;
        _autoLogoutInteractionService = autoLogoutInteractionService;
        _dialogService = dialogService ?? new AppDialogService(autoLogoutInteractionService);
        _fileDialogService = fileDialogService ?? new FileDialogService(autoLogoutInteractionService);
        DataContext = viewModel;
        InitializeComponent();

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        _viewModel.AddRequested += OnAddRequested;
        _viewModel.ImportLegacyDefinitionsRequested += OnImportLegacyDefinitionsRequested;
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
        _viewModel.ImportLegacyDefinitionsRequested -= OnImportLegacyDefinitionsRequested;
        _viewModel.EditRequested -= OnEditRequested;
    }

    private async void OnAddRequested(object? sender, EventArgs e)
    {
        var dialog = _serviceProvider.GetRequiredService<AddPartWindow>();
        await dialog.ViewModel.InitializeForCreateAsync(_viewModel.ClientAppConfigurationId, _viewModel.ResourceNumber);

        var dialogResult = _dialogService.ShowDialog(dialog, Window.GetWindow(this));

        if (dialogResult)
        {
            await _viewModel.RefreshAsync();
        }
    }

    private async void OnEditRequested(object? sender, WearPartDefinition definition)
    {
        var dialog = _serviceProvider.GetRequiredService<EditPartWindow>();
        await dialog.ViewModel.InitializeForEditAsync(definition);

        var dialogResult = _dialogService.ShowDialog(dialog, Window.GetWindow(this));

        if (dialogResult)
        {
            await _viewModel.RefreshAsync();
        }
    }

    private async void OnImportLegacyDefinitionsRequested(object? sender, EventArgs e)
    {
        var fileName = _fileDialogService.ShowOpenFileDialog(
            new OpenFileDialogRequest(
                LocalizedText.Get("ViewModels.PartManagementVm.ImportDialogTitle"),
                LocalizedText.Get("Dialogs.SQLiteDatabaseFilter")),
            Window.GetWindow(this));
        if (string.IsNullOrWhiteSpace(fileName))
        {
            _viewModel.NotifyLegacyImportCanceled();
            return;
        }

        try
        {
            var result = await _viewModel.ImportLegacyDefinitionsAsync(fileName);
            _dialogService.ShowMessage(result.ToWearPartDefinitionSummary(), LocalizedText.Get("App.LegacyImportCompletedTitle"), MessageBoxButton.OK, MessageBoxImage.Information, Window.GetWindow(this));
        }
        catch (Exception ex)
        {
            _dialogService.ShowMessage(ex.Message, LocalizedText.Get("FriendlyErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Warning, Window.GetWindow(this));
        }
    }
}
