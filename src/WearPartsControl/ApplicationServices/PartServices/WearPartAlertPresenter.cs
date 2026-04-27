using WearPartsControl.ApplicationServices.Dialogs;
using WearPartsControl.Views;

namespace WearPartsControl.ApplicationServices.PartServices;

public sealed class WearPartAlertPresenter : IWearPartAlertPresenter
{
    private readonly IAppDialogService _dialogService;

    public WearPartAlertPresenter(IAppDialogService dialogService)
    {
        _dialogService = dialogService;
    }

    public void Show(string title, string markdown)
    {
        var dialog = new WearPartAlertWindow(title, markdown);
        _dialogService.ShowDialog(dialog);
    }
}