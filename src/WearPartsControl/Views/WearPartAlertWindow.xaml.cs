using System.Windows;

namespace WearPartsControl.Views;

public partial class WearPartAlertWindow : AppDialogWindow
{
    public WearPartAlertWindow(string title, string markdown)
    {
        InitializeComponent();
        Title = title;
        AlertViewer.Document = NotificationMarkdownDocumentBuilder.Build(markdown);
    }

    private void OnCloseClicked(object sender, RoutedEventArgs e)
    {
        Close();
    }
}