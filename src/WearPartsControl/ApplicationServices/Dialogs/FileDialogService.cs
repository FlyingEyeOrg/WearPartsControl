using System.Windows;
using Microsoft.Win32;
using WearPartsControl.ApplicationServices.LoginService;

namespace WearPartsControl.ApplicationServices.Dialogs;

public sealed class FileDialogService : IFileDialogService
{
    private readonly IAutoLogoutInteractionService _autoLogoutInteractionService;

    public FileDialogService(IAutoLogoutInteractionService autoLogoutInteractionService)
    {
        _autoLogoutInteractionService = autoLogoutInteractionService;
    }

    public string? ShowOpenFileDialog(OpenFileDialogRequest request, Window? owner = null)
    {
        ArgumentNullException.ThrowIfNull(request);

        var dialog = new OpenFileDialog
        {
            Title = request.Title,
            Filter = request.Filter,
            CheckFileExists = request.CheckFileExists,
            Multiselect = request.Multiselect
        };

        var result = _autoLogoutInteractionService.RunModal(() => ShowDialog(dialog, owner));
        return result == true ? dialog.FileName : null;
    }

    public string? ShowSaveFileDialog(SaveFileDialogRequest request, Window? owner = null)
    {
        ArgumentNullException.ThrowIfNull(request);

        var dialog = new SaveFileDialog
        {
            FileName = request.FileName,
            Filter = request.Filter,
            DefaultExt = request.DefaultExt,
            AddExtension = request.AddExtension,
            OverwritePrompt = request.OverwritePrompt
        };

        var result = _autoLogoutInteractionService.RunModal(() => ShowDialog(dialog, owner));
        return result == true ? dialog.FileName : null;
    }

    private static bool? ShowDialog(FileDialog dialog, Window? owner)
    {
        return owner is not null && owner.IsVisible && owner.WindowState != WindowState.Minimized
            ? dialog.ShowDialog(owner)
            : dialog.ShowDialog();
    }
}