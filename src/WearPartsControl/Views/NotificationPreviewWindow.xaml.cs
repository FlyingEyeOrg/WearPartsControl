using System.Windows;

namespace WearPartsControl.Views;

public partial class NotificationPreviewWindow : AppDialogWindow
{
    public NotificationPreviewWindow(string warningMarkdown, string shutdownMarkdown)
    {
        InitializeComponent();
        WarningPreviewTextBox.Text = warningMarkdown;
        ShutdownPreviewTextBox.Text = shutdownMarkdown;
    }

    private void OnCloseClicked(object sender, RoutedEventArgs e)
    {
        Close();
    }
}