using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using WearPartsControl.UserControls;
using Xunit;

namespace WearPartsControl.Tests;

[Collection(UserTabControlTestCollection.Name)]
public sealed class UserTabControlTests
{
    [Fact]
    public void HeadersChange_ShouldReselectCurrentTab_UsingUpdatedItems()
    {
        RunWpfTest(() =>
        {
            var command = new RecordingCommand();
            using var host = UserTabControlHost.Create(new[] { "A", "B", "C" }, 1, command);

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
        RunWpfTest(() =>
        {
            var command = new RecordingCommand();
            using var host = UserTabControlHost.Create(new[] { "Same", "Same", "Other" }, 0, command);

            var secondButton = host.GetButton(1);
            secondButton.IsChecked = true;
            host.DrainDispatcher();

            Assert.Equal(1, host.Control.TabIndex);
            Assert.Equal(new[] { 0, 1 }, command.ExecutedIndexes);
            Assert.True(host.GetButton(1).IsChecked);
            Assert.Equal("Same", host.GetButtonText(1));
        });
    }

    private static void RunWpfTest(Action action)
    {
        Exception? exception = null;

        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                exception = ex;
            }
            finally
            {
                Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (exception != null)
        {
            ExceptionDispatchInfo.Capture(exception).Throw();
        }
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

    private sealed class UserTabControlHost : IDisposable
    {
        private readonly Window _window;
        private readonly ItemsControl _itemsControl;

        private UserTabControlHost(Window window, UserTabControl control, ItemsControl itemsControl)
        {
            _window = window;
            Control = control;
            _itemsControl = itemsControl;
        }

        public UserTabControl Control { get; }

        public static UserTabControlHost Create(IEnumerable<string> headers, int tabIndex, ICommand command)
        {
            var control = new UserTabControl
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
            DrainDispatcherCore();
            window.UpdateLayout();
            DrainDispatcherCore();

            var itemsControl = (ItemsControl)control.FindName("TabItemsControl")!;
            return new UserTabControlHost(window, control, itemsControl);
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
            DrainDispatcherCore();
        }

        public void Dispose()
        {
            _window.Close();
            DrainDispatcherCore();
        }

        private static void DrainDispatcherCore()
        {
            var frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new DispatcherOperationCallback(_ =>
            {
                frame.Continue = false;
                return null;
            }), null);
            Dispatcher.PushFrame(frame);
        }
    }
}

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class UserTabControlTestCollection
{
    public const string Name = "UserTabControl.Wpf";
}
