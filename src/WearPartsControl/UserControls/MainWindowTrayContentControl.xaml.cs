using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace WearPartsControl.UserControls;

public partial class MainWindowTrayContentControl : UserControl
{
    public static readonly DependencyProperty RestoreCommandProperty = DependencyProperty.Register(
        nameof(RestoreCommand),
        typeof(ICommand),
        typeof(MainWindowTrayContentControl),
        new PropertyMetadata(null));

    public static readonly DependencyProperty ExitCommandProperty = DependencyProperty.Register(
        nameof(ExitCommand),
        typeof(ICommand),
        typeof(MainWindowTrayContentControl),
        new PropertyMetadata(null));

    public MainWindowTrayContentControl()
    {
        InitializeComponent();
    }

    public ICommand? RestoreCommand
    {
        get => (ICommand?)GetValue(RestoreCommandProperty);
        set => SetValue(RestoreCommandProperty, value);
    }

    public ICommand? ExitCommand
    {
        get => (ICommand?)GetValue(ExitCommandProperty);
        set => SetValue(ExitCommandProperty, value);
    }
}