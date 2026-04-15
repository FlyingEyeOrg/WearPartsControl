using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace WearPartsControl.UserControls
{
    /// <summary>
    /// UserTabControl.xaml 的交互逻辑
    /// </summary>
    public partial class UserTabControl : UserControl
    {
        // 说明 (简要):
        // - 在模板中我们通过 ItemsControl.AlternationIndex 将容器索引暴露到模板内的控件(Tag)，
        //   这样可以在点击时 O(1) 读取项索引，避免运行时遍历视觉树或依赖 header 唯一性。
        // - 当外部替换 `Headers` 时，容器需要时间重新生成；因此我们在 Headers 变更时
        //   延迟到 DispatcherPriority.Loaded 再执行 ApplySelection，以确保容器已经可用。
        // - 将 PrimaryBrush 使用 DynamicResource 而非 StaticResource，降低对宿主资源初始化顺序的耦合，
        //   这也让控件更容易在测试环境中加载。

        private readonly DependencyPropertyDescriptor _tabIndexDescriptor;
        private bool _isLoaded;
        private bool _isTabIndexChangedSubscribed;
        private bool _isUpdatingSelection;
        private ToggleButton? _selectedButton;

        public UserTabControl()
        {
            InitializeComponent();

            _tabIndexDescriptor = DependencyPropertyDescriptor.FromProperty(Control.TabIndexProperty, typeof(UserTabControl));

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        public IEnumerable<string> Headers
        {
            get => (IEnumerable<string>)GetValue(HeadersProperty);
            set => SetValue(HeadersProperty, value);
        }

        public static readonly DependencyProperty HeadersProperty =
            DependencyProperty.Register(
                nameof(Headers),
                typeof(IEnumerable<string>),
                typeof(UserTabControl),
                new PropertyMetadata(new List<string>(), OnHeadersChanged));

        public ICommand? Command
        {
            get => (ICommand?)GetValue(CommandProperty);
            set => SetValue(CommandProperty, value);
        }

        public static readonly DependencyProperty CommandProperty =
            DependencyProperty.Register(nameof(Command), typeof(ICommand), typeof(UserTabControl), new PropertyMetadata(null));

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _isLoaded = true;
            SubscribeToTabIndexChanged();
            ApplySelection(TabIndex, invokeCommand: true);
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            _isLoaded = false;
            UnsubscribeFromTabIndexChanged();
        }

        private static void OnHeadersChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
        {
            if (dependencyObject is not UserTabControl control)
            {
                return;
            }

            control.ClearSelection();

            if (!control._isLoaded)
            {
                return;
            }

            control.Dispatcher.BeginInvoke(
                DispatcherPriority.Loaded,
                new Action(() => control.ApplySelection(control.TabIndex, invokeCommand: true)));
        }

        private void OnTabIndexChanged(object? sender, EventArgs e)
        {
            ApplySelection(TabIndex, invokeCommand: _isLoaded);
        }

        private void TabItemButton_Checked(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingSelection || sender is not ToggleButton button)
            {
                return;
            }

            var index = GetItemIndex(button);
            if (index < 0)
            {
                return;
            }

            if (TabIndex != index)
            {
                TabIndex = index;
            }
            else if (_isLoaded)
            {
                ExecuteCommand(index);
            }
        }

        private void TabItemButton_Unchecked(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingSelection || sender is not ToggleButton button)
            {
                return;
            }

            if (ReferenceEquals(button, _selectedButton))
            {
                _isUpdatingSelection = true;
                button.IsChecked = true;
                _isUpdatingSelection = false;
            }
        }

        private void ApplySelection(int index, bool invokeCommand)
        {
            var headers = Headers?.ToList() ?? new List<string>();
            if (headers.Count == 0)
            {
                return;
            }

            if (index < 0)
            {
                ClearSelection();
                return;
            }

            var normalizedIndex = NormalizeIndex(index, headers.Count);
            var selectedButton = FindButtonByIndex(normalizedIndex);
            if (selectedButton == null)
            {
                return;
            }

            _isUpdatingSelection = true;
            try
            {
                if (_selectedButton != null && !ReferenceEquals(_selectedButton, selectedButton))
                {
                    _selectedButton.IsChecked = false;
                }

                selectedButton.IsChecked = true;
                _selectedButton = selectedButton;
            }
            finally
            {
                _isUpdatingSelection = false;
            }

            if (invokeCommand)
            {
                ExecuteCommand(normalizedIndex);
            }
        }

        private void ClearSelection()
        {
            _isUpdatingSelection = true;
            try
            {
                if (_selectedButton != null)
                {
                    _selectedButton.IsChecked = false;
                    _selectedButton = null;
                }
            }
            finally
            {
                _isUpdatingSelection = false;
            }
        }

        private void ExecuteCommand(int index)
        {
            if (Command is null)
            {
                return;
            }

            if (Command.CanExecute(index))
            {
                Command.Execute(index);
            }
        }

        private void SubscribeToTabIndexChanged()
        {
            if (_isTabIndexChangedSubscribed)
            {
                return;
            }

            _tabIndexDescriptor.AddValueChanged(this, OnTabIndexChanged);
            _isTabIndexChangedSubscribed = true;
        }

        private void UnsubscribeToTabIndexChangedCore()
        {
            _tabIndexDescriptor.RemoveValueChanged(this, OnTabIndexChanged);
            _isTabIndexChangedSubscribed = false;
        }

        private void UnsubscribeFromTabIndexChanged()
        {
            if (!_isTabIndexChangedSubscribed)
            {
                return;
            }

            UnsubscribeToTabIndexChangedCore();
        }

        private int NormalizeIndex(int index, int count)
        {
            if (count <= 0)
            {
                return -1;
            }

            if (index >= count)
            {
                return count - 1;
            }

            return index;
        }

        private int GetItemIndex(DependencyObject element)
        {
            if (element is FrameworkElement frameworkElement && frameworkElement.Tag is int index)
            {
                return index;
            }

            return -1;
        }

        private ToggleButton? FindButtonByIndex(int index)
        {
            var container = TabItemsControl.ItemContainerGenerator.ContainerFromIndex(index) as ContentPresenter;
            if (container is null)
            {
                return null;
            }

            container.ApplyTemplate();
            return container.ContentTemplate?.FindName("TabButton", container) as ToggleButton;
        }
    }
}