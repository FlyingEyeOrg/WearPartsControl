namespace WearPartsControl.ApplicationServices.Dialogs;

public sealed record SaveFileDialogRequest(
    string FileName,
    string Filter,
    string DefaultExt,
    bool AddExtension = true,
    bool OverwritePrompt = true);