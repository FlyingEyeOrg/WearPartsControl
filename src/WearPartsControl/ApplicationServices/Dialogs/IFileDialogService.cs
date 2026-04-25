using System.Windows;

namespace WearPartsControl.ApplicationServices.Dialogs;

public interface IFileDialogService
{
    string? ShowOpenFileDialog(OpenFileDialogRequest request, Window? owner = null);

    string? ShowSaveFileDialog(SaveFileDialogRequest request, Window? owner = null);
}