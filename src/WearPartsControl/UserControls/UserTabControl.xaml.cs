using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace WearPartsControl.UserControls
{
    /// <summary>
    /// UserTabControl.xaml 的交互逻辑
    /// </summary>
    public partial class UserTabControl : UserControl
    {
        private readonly DependencyPropertyDescriptor _tabIndexDescriptor;
        private bool _isLoaded;
        private bool _isUpdatingSelection;
        private ToggleButton? _selectedButton;

        public UserTabControl()
        {
            InitializeComponent();

            _tabIndexDescriptor = DependencyPropertyDescriptor.FromProperty(Control.TabIndexProperty, typeof(UserTabControl));
            _tabIndexDescriptor.AddValueChanged(this, OnTabIndexChanged);

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        public IEnumerable<string> Headers
        {
            get => (IEnumerable<string>)GetValue(HeadersProperty);
            set => SetValue(HeadersProperty, value);
        }

        public static readonly DependencyProperty HeadersProperty =
            DependencyProperty.Register(nameof(Headers), typeof(IEnumerable<string>), typeof(UserTabControl), new PropertyMetadata(new List<string>()));

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
            ApplySelection(TabIndex, invokeCommand: true);
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            _isLoaded = false;
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
                ExecuteCommand(GetHeader(index));
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
                ExecuteCommand(headers[normalizedIndex]);
            }
        }

        private void ExecuteCommand(string header)
        {
            if (Command is null)
            {
                return;
            }

            if (Command.CanExecute(header))
            {
                Command.Execute(header);
            }
        }

        private int NormalizeIndex(int index, int count)
        {
            if (count <= 0)
            {
                return -1;
            }

            if (index < 0)
            {
                return 0;
            }

            if (index >= count)
            {
                return count - 1;
            }

            return index;
        }

        private int GetItemIndex(DependencyObject element)
        {
            var container = ItemsControl.ContainerFromElement(TabItemsControl, element);
            if (container is null)
            {
                return -1;
            }

            return TabItemsControl.ItemContainerGenerator.IndexFromContainer(container);
        }

        private ToggleButton? FindButtonByIndex(int index)
        {
            var container = TabItemsControl.ItemContainerGenerator.ContainerFromIndex(index) as DependencyObject;
            if (container is null)
            {
                return null;
            }

            return FindDescendant<ToggleButton>(container);
        }

        private string GetHeader(int index)
        {
            return Headers.ElementAt(index);
        }

        private static T? FindDescendant<T>(DependencyObject root) where T : DependencyObject
        {
            var childCount = VisualTreeHelper.GetChildrenCount(root);
            for (var i = 0; i < childCount; i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                if (child is T match)
                {
                    return match;
                }

                var descendant = FindDescendant<T>(child);
                if (descendant != null)
                {
                    return descendant;
                }
            }

            return null;
        }
    }
}