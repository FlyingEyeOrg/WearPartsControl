using System.Windows;
using WearPartsControl.Views;

namespace WearPartsControl.ApplicationServices.PartServices;

public sealed class WearPartAlertPresenter : IWearPartAlertPresenter
{
    public void Show(string title, string markdown)
    {
        var dialog = new WearPartAlertWindow(title, markdown);
        dialog.Owner = null;
        dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        dialog.Show();
    }
}
