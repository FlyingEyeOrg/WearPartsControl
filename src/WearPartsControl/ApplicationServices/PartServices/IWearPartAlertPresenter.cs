namespace WearPartsControl.ApplicationServices.PartServices;

public interface IWearPartAlertPresenter
{
    void Show(string title, string markdown);
}