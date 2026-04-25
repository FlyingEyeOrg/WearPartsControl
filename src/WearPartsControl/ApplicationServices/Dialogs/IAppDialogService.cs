using System.Windows;

namespace WearPartsControl.ApplicationServices.Dialogs;

public interface IAppDialogService
{
    bool ShowDialog(Window dialog, Window? owner = null);

    MessageBoxResult ShowMessage(
        string message,
        string title,
        MessageBoxButton buttons = MessageBoxButton.OK,
        MessageBoxImage image = MessageBoxImage.None,
        Window? owner = null,
        MessageBoxResult defaultResult = MessageBoxResult.None);
}