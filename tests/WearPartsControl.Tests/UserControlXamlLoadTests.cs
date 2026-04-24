using System;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using WearPartsControl.ApplicationServices.LoginService;
using WearPartsControl.UserControls;
using WearPartsControl.ViewModels;
using Xunit;

namespace WearPartsControl.Tests;

[Collection(UserTabControlTestCollection.Name)]
public sealed class UserControlXamlLoadTests
{
    [Fact]
    public void ReplacePartUserControl_ShouldLoadWithoutXamlParseException()
    {
        RunWpfTest(() =>
        {
            var control = new ReplacePartUserControl(
                CreateUninitialized<ReplacePartViewModel>(),
                new StubAutoLogoutInteractionService());

            Assert.NotNull(control);
        });
    }

    [Fact]
    public void PartManagementUserControl_ShouldLoadWithoutXamlParseException()
    {
        RunWpfTest(() =>
        {
            var control = new PartManagementUserControl(
                CreateUninitialized<PartManagementViewModel>(),
                new StubServiceProvider(),
                new StubAutoLogoutInteractionService());

            Assert.NotNull(control);
        });
    }

    [Fact]
    public void PartUpdateRecordUserControl_ShouldLoadWithoutXamlParseException()
    {
        RunWpfTest(() =>
        {
            var control = new PartUpdateRecordUserControl(
                CreateUninitialized<PartUpdateRecordViewModel>(),
                new StubAutoLogoutInteractionService());

            Assert.NotNull(control);
        });
    }

    private static T CreateUninitialized<T>() where T : class
    {
        return (T)RuntimeHelpers.GetUninitializedObject(typeof(T));
    }

    private static void RunWpfTest(Action action)
    {
        Exception? exception = null;

        var thread = new Thread(() =>
        {
            var ownsApplication = false;

            try
            {
                ownsApplication = EnsureApplicationResources();
                action();
                DrainDispatcher();
            }
            catch (Exception ex)
            {
                exception = ex;
            }
            finally
            {
                if (ownsApplication && Application.Current is not null)
                {
                    Application.Current.Shutdown();
                }

                Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (exception is not null)
        {
            ExceptionDispatchInfo.Capture(exception).Throw();
        }
    }

    private static bool EnsureApplicationResources()
    {
        var ownsApplication = false;

        if (Application.Current is null)
        {
            _ = new Application();
            ownsApplication = true;
        }

        var resources = Application.Current!.Resources;
        if (resources.Contains("ButtonPrimary"))
        {
            return ownsApplication;
        }

        resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("pack://application:,,,/HandyControl;component/Themes/SkinDefault.xaml", UriKind.Absolute)
        });
        resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("pack://application:,,,/HandyControl;component/Themes/Theme.xaml", UriKind.Absolute)
        });
        resources["BooleanToVisibilityConverter"] = new BooleanToVisibilityConverter();
        return ownsApplication;
    }

    private static void DrainDispatcher()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new DispatcherOperationCallback(_ =>
        {
            frame.Continue = false;
            return null;
        }), null);
        Dispatcher.PushFrame(frame);
    }

    private sealed class StubAutoLogoutInteractionService : IAutoLogoutInteractionService
    {
        public void NotifyActivity()
        {
        }

        public TResult RunModal<TResult>(Func<TResult> interaction)
        {
            return interaction();
        }
    }

    private sealed class StubServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType)
        {
            return null;
        }
    }
}