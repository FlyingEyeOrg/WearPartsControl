using System.Windows;

namespace WearPartsControl.Views;

public partial class NotificationPreviewWindow : AppDialogWindow
{
    public NotificationPreviewWindow(string warningMarkdown, string shutdownMarkdown)
    {
        InitializeComponent();
        WarningPreviewViewer.Document = NotificationMarkdownDocumentBuilder.Build(warningMarkdown);
        ShutdownPreviewViewer.Document = NotificationMarkdownDocumentBuilder.Build(shutdownMarkdown);
    }

    private void OnCloseClicked(object sender, RoutedEventArgs e)
    {
        Close();
    }
}