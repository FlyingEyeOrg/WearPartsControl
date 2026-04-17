using System.Windows;
using System.Windows.Controls;
using WearPartsControl.ViewModels;

namespace WearPartsControl.UserControls;

/// <summary>
/// PartInfoUserControl.xaml 的交互逻辑
/// </summary>
public partial class PartInfoUserControl : UserControl
{
    public static readonly DependencyProperty EditorProperty = DependencyProperty.Register(
        nameof(Editor),
        typeof(WearPartEditorViewModelBase),
        typeof(PartInfoUserControl),
        new PropertyMetadata(null));

    public PartInfoUserControl()
    {
        InitializeComponent();
    }

    public WearPartEditorViewModelBase? Editor
    {
        get => (WearPartEditorViewModelBase?)GetValue(EditorProperty);
        set => SetValue(EditorProperty, value);
    }
}
