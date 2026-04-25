using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace WearPartsControl.Views;

public partial class MessageDialogWindow : AppDialogWindow
{
    private MessageBoxResult _result;
    private readonly MessageBoxButton _buttons;
    private bool _hasExplicitResult;

    public MessageDialogWindow(
        string message,
        string title,
        MessageBoxButton buttons,
        MessageBoxImage image,
        MessageBoxResult defaultResult = MessageBoxResult.None)
    {
        InitializeComponent();

        _buttons = buttons;
        _result = defaultResult != MessageBoxResult.None
            ? defaultResult
            : GetFallbackResult(buttons);

        Title = title;
        MessageTextBlock.Text = message;

        ConfigureVisual(image);
        ConfigureButtons(buttons);
        Closing += OnDialogClosing;
    }

    public MessageBoxResult Result => _result;

    public static MessageBoxResult ShowMessage(
        Window? owner,
        string message,
        string title,
        MessageBoxButton buttons,
        MessageBoxImage image,
        MessageBoxResult defaultResult = MessageBoxResult.None)
    {
        var dialog = new MessageDialogWindow(message, title, buttons, image, defaultResult);
        ConfigureOwner(dialog, owner);
        dialog.ShowDialog();
        return dialog.Result;
    }

    private static void ConfigureOwner(Window dialog, Window? owner)
    {
        if (owner is not null && owner.IsVisible && owner.WindowState != WindowState.Minimized)
        {
            dialog.Owner = owner;
            dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            return;
        }

        dialog.Owner = null;
        dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
    }

    private void ConfigureVisual(MessageBoxImage image)
    {
        var brush = image switch
        {
            MessageBoxImage.Error => CreateBrush("#D64C4C"),
            MessageBoxImage.Warning => CreateBrush("#D88B22"),
            MessageBoxImage.Question => CreateBrush("#3C6C97"),
            MessageBoxImage.Information => CreateBrush("#2D8F78"),
            _ => CreateBrush("#3C6C97")
        };

        AccentBar.Background = brush;
    }

    private void ConfigureButtons(MessageBoxButton buttons)
    {
        var labels = GetButtonLabels();

        switch (buttons)
        {
            case MessageBoxButton.OK:
                SetButton(PrimaryButton, labels.Ok, MessageBoxResult.OK);
                break;
            case MessageBoxButton.OKCancel:
                SetButton(PrimaryButton, labels.Ok, MessageBoxResult.OK);
                SetButton(SecondaryButton, labels.Cancel, MessageBoxResult.Cancel);
                break;
            case MessageBoxButton.YesNo:
                SetButton(PrimaryButton, labels.Yes, MessageBoxResult.Yes);
                SetButton(SecondaryButton, labels.No, MessageBoxResult.No);
                break;
            case MessageBoxButton.YesNoCancel:
                SetButton(PrimaryButton, labels.Yes, MessageBoxResult.Yes);
                SetButton(SecondaryButton, labels.No, MessageBoxResult.No);
                SetButton(TertiaryButton, labels.Cancel, MessageBoxResult.Cancel);
                break;
        }
    }

    private static void SetButton(Button button, string text, MessageBoxResult result)
    {
        button.Content = text;
        button.Tag = result;
        button.Visibility = Visibility.Visible;
    }

    private void OnPrimaryButtonClick(object sender, RoutedEventArgs e)
    {
        CloseWith((MessageBoxResult)PrimaryButton.Tag);
    }

    private void OnSecondaryButtonClick(object sender, RoutedEventArgs e)
    {
        CloseWith((MessageBoxResult)SecondaryButton.Tag);
    }

    private void OnTertiaryButtonClick(object sender, RoutedEventArgs e)
    {
        CloseWith((MessageBoxResult)TertiaryButton.Tag);
    }

    private void CloseWith(MessageBoxResult result)
    {
        _hasExplicitResult = true;
        _result = result;
        DialogResult = result is MessageBoxResult.OK or MessageBoxResult.Yes;
        Close();
    }

    private void OnDialogClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_hasExplicitResult)
        {
            return;
        }

        _result = GetCloseResult(_buttons, _result);
    }

    private static MessageBoxResult GetFallbackResult(MessageBoxButton buttons)
    {
        return buttons switch
        {
            MessageBoxButton.OK => MessageBoxResult.OK,
            MessageBoxButton.OKCancel => MessageBoxResult.Cancel,
            MessageBoxButton.YesNo => MessageBoxResult.No,
            MessageBoxButton.YesNoCancel => MessageBoxResult.Cancel,
            _ => MessageBoxResult.None
        };
    }

    private static MessageBoxResult GetCloseResult(MessageBoxButton buttons, MessageBoxResult fallbackResult)
    {
        return buttons switch
        {
            MessageBoxButton.OKCancel => MessageBoxResult.Cancel,
            MessageBoxButton.YesNoCancel => MessageBoxResult.Cancel,
            _ => fallbackResult
        };
    }

    private static (string Ok, string Cancel, string Yes, string No) GetButtonLabels()
    {
        var isChinese = CultureInfo.CurrentUICulture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase);
        return isChinese
            ? ("确定", "取消", "是", "否")
            : ("OK", "Cancel", "Yes", "No");
    }

    private static Brush CreateBrush(string hex)
    {
        return (Brush)new BrushConverter().ConvertFromString(hex)!;
    }
}