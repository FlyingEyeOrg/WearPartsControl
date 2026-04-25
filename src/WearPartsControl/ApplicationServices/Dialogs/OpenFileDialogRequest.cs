namespace WearPartsControl.ApplicationServices.Dialogs;

public sealed record OpenFileDialogRequest(
    string Title,
    string Filter,
    bool CheckFileExists = true,
    bool Multiselect = false);