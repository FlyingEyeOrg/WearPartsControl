using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace WearPartsControl.UserControls
{
    /// <summary>
    /// UserTabControl.xaml 的交互逻辑
    /// </summary>
    public partial class UserTabControl : UserControl
    {
        private readonly Brush _primaryBrush;

        public UserTabControl()
        {
            _primaryBrush = (Brush)this.FindResource("PrimaryBrush");
            InitializeComponent();
        }

        public IEnumerable<string> Headers
        {
            get { return (IEnumerable<string>)GetValue(HeadersProperty); }
            set { SetValue(HeadersProperty, value); }
        }

        // Using a DependencyProperty as the backing store for ItemsSource. This enables animation, styling, binding, etc...
        public static readonly DependencyProperty HeadersProperty =
            DependencyProperty.Register("Headers", typeof(IEnumerable<string>), typeof(UserTabControl), new PropertyMetadata(new List<string>()));

        private Border? _selectedBorder = null;

        private void Grid_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is Border border)
            {
                if (border == _selectedBorder)
                {
                    return;
                }

                if (border != _selectedBorder)
                {
                    border.Background = _primaryBrush;
                    var textBlock = (TextBlock)border.FindName("TextContent");
                    textBlock.Foreground = Brushes.White;
                }

                if (_selectedBorder != null)
                {
                    _selectedBorder.Background = Brushes.AliceBlue;
                    var textBlock = (TextBlock)_selectedBorder.FindName("TextContent");
                    textBlock.Foreground = Brushes.Black;
                }

                _selectedBorder = border;
            }
        }
    }
}
