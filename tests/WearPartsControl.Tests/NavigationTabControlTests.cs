using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using WearPartsControl.UserControls;
using Xunit;

namespace WearPartsControl.Tests;

[Collection(NavigationTabControlTestCollection.Name)]
public sealed class NavigationTabControlTests
{
    [Fact]
    public void HeadersChange_ShouldReselectCurrentTab_UsingUpdatedItems()
    {
        WpfTestHost.Run(() =>
        {
            var command = new RecordingCommand();
            using var host = NavigationTabControlHost.Create(new[] { "A", "B", "C" }, 1, command);

            Assert.Equal(1, host.Control.TabIndex);
            Assert.Equal(new[] { 1 }, command.ExecutedIndexes);
            Assert.True(host.GetButton(1).IsChecked);

            host.Control.Headers = new[] { "X", "Y", "Z" };
            host.DrainDispatcher();

            Assert.Equal(1, host.Control.TabIndex);
            Assert.Equal(new[] { 1, 1 }, command.ExecutedIndexes);
            Assert.True(host.GetButton(1).IsChecked);
            Assert.False(host.GetButton(0).IsChecked ?? false);
            Assert.Equal("Y", host.GetButtonText(1));
        });
    }

    [Fact]
    public void ClickingDuplicateHeaders_ShouldExecuteCommandWithActualIndex()
    {
        WpfTestHost.Run(() =>
        {
            var command = new RecordingCommand();
            using var host = NavigationTabControlHost.Create(new[] { "Same", "Same", "Other" }, 0, command);

            var secondButton = host.GetButton(1);
            secondButton.IsChecked = true;
            host.DrainDispatcher();

            Assert.Equal(1, host.Control.TabIndex);
            Assert.Equal(new[] { 0, 1 }, command.ExecutedIndexes);
            Assert.True(host.GetButton(1).IsChecked);
            Assert.Equal("Same", host.GetButtonText(1));
        });
    }

    private sealed class RecordingCommand : ICommand
    {
        public List<int> ExecutedIndexes { get; } = new();

        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter)
        {
            return parameter is int;
        }

        public void Execute(object? parameter)
        {
            ExecutedIndexes.Add((int)parameter!);
        }
    }

    private sealed class NavigationTabControlHost : IDisposable
    {
        private readonly Window _window;
        private readonly ItemsControl _itemsControl;

        private NavigationTabControlHost(Window window, NavigationTabControl control, ItemsControl itemsControl)
        {
            _window = window;
            Control = control;
            _itemsControl = itemsControl;
        }

        public NavigationTabControl Control { get; }

        public static NavigationTabControlHost Create(IEnumerable<string> headers, int tabIndex, ICommand command)
        {
            var control = new NavigationTabControl
            {
                Headers = headers,
                TabIndex = tabIndex,
                Command = command
            };
            control.Resources["PrimaryBrush"] = Brushes.DodgerBlue;

            var window = new Window
            {
                Width = 640,
                Height = 480,
                Content = control
            };

            window.Show();
            WpfTestHost.DrainDispatcher();
            window.UpdateLayout();
            WpfTestHost.DrainDispatcher();

            var itemsControl = (ItemsControl)control.FindName("TabItemsControl")!;
            return new NavigationTabControlHost(window, control, itemsControl);
        }

        public ToggleButton GetButton(int index)
        {
            _itemsControl.UpdateLayout();
            var presenter = (ContentPresenter)_itemsControl.ItemContainerGenerator.ContainerFromIndex(index)!;
            presenter.ApplyTemplate();
            return (ToggleButton)presenter.ContentTemplate.FindName("TabButton", presenter)!;
        }

        public string GetButtonText(int index)
        {
            return ((TextBlock)((Border)GetButton(index).Content).Child).Text;
        }

        public void DrainDispatcher()
        {
            WpfTestHost.DrainDispatcher();
        }

        public void Dispose()
        {
            _window.Close();
            WpfTestHost.DrainDispatcher();
        }
    }
}

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class NavigationTabControlTestCollection
{
    public const string Name = "NavigationTabControl.Wpf";
}
