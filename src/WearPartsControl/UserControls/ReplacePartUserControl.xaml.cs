using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using WearPartsControl.ApplicationServices.Dialogs;
using WearPartsControl.ApplicationServices.LoginService;
using WearPartsControl.ApplicationServices.Localization;
using WearPartsControl.ViewModels;

namespace WearPartsControl.UserControls
{
    /// <summary>
    /// ReplacePartUserControl.xaml 的交互逻辑
    /// </summary>
    public partial class ReplacePartUserControl : UserControl
    {
        private readonly IAutoLogoutInteractionService _autoLogoutInteractionService;
        private readonly IAppDialogService _dialogService;
        private bool _isInitialized;

        public ReplacePartUserControl(ReplacePartViewModel viewModel, IAutoLogoutInteractionService autoLogoutInteractionService, IAppDialogService? dialogService = null)
        {
            InitializeComponent();
            _autoLogoutInteractionService = autoLogoutInteractionService;
            _dialogService = dialogService ?? new AppDialogService(autoLogoutInteractionService);
            DataContext = viewModel;
            Loaded += OnLoaded;
        }

        private async void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
        {
            if (DataContext is not ReplacePartViewModel viewModel)
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

        private async void OnReplaceClicked(object sender, RoutedEventArgs e)
        {
            if (DataContext is not ReplacePartViewModel viewModel || !viewModel.IsReplaceEnabled)
            {
                return;
            }

            var owner = Window.GetWindow(this);
            var context = viewModel.GetReplacementConfirmationContext();
            var title = LocalizedText.Get("ViewModels.ReplacePartVm.ReplaceConfirmTitle");
            var message = context.IsReturningOldPart
                ? LocalizedText.Format("ViewModels.ReplacePartVm.ReplaceConfirmReturningOldPartMessage", context.PartName, context.Barcode)
                : LocalizedText.Format("ViewModels.ReplacePartVm.ReplaceConfirmMessage", context.PartName, context.Barcode);

            var confirmed = _dialogService.ShowMessage(message, title, MessageBoxButton.YesNo, MessageBoxImage.Warning, owner) == MessageBoxResult.Yes;
            if (!confirmed)
            {
                return;
            }

            if (context.IsReturningOldPart && context.HasReachedWarningLifetime)
            {
                var warningConfirmed = _dialogService.ShowMessage(
                    LocalizedText.Format(
                        "ViewModels.ReplacePartVm.ReturningOldPartWarningConfirmMessage",
                        context.Barcode,
                        context.CurrentValueText,
                        context.WarningValueText,
                        context.ShutdownValueText),
                    title,
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning,
                    owner) == MessageBoxResult.Yes;
                if (!warningConfirmed)
                {
                    return;
                }
            }

            await viewModel.ReplaceCommand.ExecuteAsync(null);
        }
    }
}
