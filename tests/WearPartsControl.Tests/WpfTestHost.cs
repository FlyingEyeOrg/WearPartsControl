using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace WearPartsControl.Tests;

internal static class WpfTestHost
{
    private static readonly object SyncRoot = new();
    private static Application? _application;
    private static Dispatcher? _dispatcher;

    public static void Run(Action action, bool ensureApplicationResources = false)
    {
        EnsureStarted(ensureApplicationResources);

        ExceptionDispatchInfo? capturedException = null;
        _dispatcher!.Invoke(() =>
        {
            try
            {
                if (ensureApplicationResources)
                {
                    EnsureApplicationResources();
                }

                action();
                DrainDispatcher();
            }
            catch (Exception ex)
            {
                capturedException = ExceptionDispatchInfo.Capture(ex);
            }
        });

        capturedException?.Throw();
    }

    public static void DrainDispatcher()
    {
        EnsureStarted(ensureApplicationResources: false);
        _dispatcher!.Invoke(() =>
        {
            var frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new DispatcherOperationCallback(_ =>
            {
                frame.Continue = false;
                return null;
            }), null);
            Dispatcher.PushFrame(frame);
        });
    }

    private static void EnsureStarted(bool ensureApplicationResources)
    {
        lock (SyncRoot)
        {
            if (_dispatcher is not null)
            {
                if (ensureApplicationResources)
                {
                    _dispatcher.Invoke(EnsureApplicationResources);
                }

                return;
            }

            var started = new ManualResetEventSlim(false);
            var thread = new Thread(() =>
            {
                _application = new Application
                {
                    ShutdownMode = ShutdownMode.OnExplicitShutdown
                };
                _dispatcher = Dispatcher.CurrentDispatcher;

                if (ensureApplicationResources)
                {
                    EnsureApplicationResources();
                }

                started.Set();
                Dispatcher.Run();
            });

            thread.IsBackground = true;
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            started.Wait();
        }
    }

    private static void EnsureApplicationResources()
    {
        if (_application is null)
        {
            _application = new Application
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown
            };
        }

        var resources = _application.Resources;
        if (resources.Contains("ButtonPrimary"))
        {
            return;
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
    }
}