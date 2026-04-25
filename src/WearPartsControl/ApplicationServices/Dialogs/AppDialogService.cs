using System.Windows;
using WearPartsControl.ApplicationServices.LoginService;
using WearPartsControl.Views;

namespace WearPartsControl.ApplicationServices.Dialogs;

public sealed class AppDialogService : IAppDialogService
{
    private readonly IAutoLogoutInteractionService _autoLogoutInteractionService;

    public AppDialogService(IAutoLogoutInteractionService autoLogoutInteractionService)
    {
        _autoLogoutInteractionService = autoLogoutInteractionService;
    }

    public bool ShowDialog(Window dialog, Window? owner = null)
    {
        ArgumentNullException.ThrowIfNull(dialog);

        ConfigureOwner(dialog, owner);
        return _autoLogoutInteractionService.RunModal(() => dialog.ShowDialog() == true);
    }

    public MessageBoxResult ShowMessage(
        string message,
        string title,
        MessageBoxButton buttons = MessageBoxButton.OK,
        MessageBoxImage image = MessageBoxImage.None,
        Window? owner = null,
        MessageBoxResult defaultResult = MessageBoxResult.None)
    {
        return _autoLogoutInteractionService.RunModal(() =>
            MessageDialogWindow.ShowMessage(owner, message, title, buttons, image, defaultResult));
    }

    private static void ConfigureOwner(Window dialog, Window? owner)
    {
        if (owner is not null && owner.IsVisible && owner.WindowState != WindowState.Minimized)
        {
            dialog.Owner = owner;
            dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            return;
        }

        dialog.Owner = null;
        dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
    }
}