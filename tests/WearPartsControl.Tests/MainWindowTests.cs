using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using HandyControl.Controls;
using WearPartsControl.ApplicationServices;
using WearPartsControl.ApplicationServices.AppSettings;
using WearPartsControl.ApplicationServices.ClientAppInfo;
using WearPartsControl.ApplicationServices.Localization;
using WearPartsControl.ApplicationServices.Localization.Generated;
using WearPartsControl.ApplicationServices.LoginService;
using WearPartsControl.ApplicationServices.PlcService;
using WearPartsControl.ApplicationServices.Startup;
using WearPartsControl.ApplicationServices.UserConfig;
using WearPartsControl.UserControls;
using WearPartsControl.ViewModels;
using WearPartsControl.Views;
using Xunit;

namespace WearPartsControl.Tests;

[Collection(UserTabControlTestCollection.Name)]
public sealed class MainWindowTests
{
    [Fact]
    public void Show_ShouldLoadWithoutXamlParseException()
    {
        using var cultureScope = new TestCultureScope("en-US");

        WpfTestHost.Run(() =>
        {
            var currentUserAccessor = new CurrentUserAccessor();
            var loginService = new StubLoginService();
            var viewModel = new MainWindowViewModel(
                new StubLocalizationService(),
                new StubServiceProvider(),
                loginService,
                new StubAppSettingsService(),
                new StubUserConfigService(),
                new StubClientAppInfoService(),
                new UiBusyService(TimeSpan.Zero),
                new StubPlcStartupConnectionService(),
                new LoginSessionStateMachine(currentUserAccessor, loginService),
                new StubUiDispatcher(),
                new StubAppStartupCoordinator());

            var window = new MainWindow(viewModel, new StubServiceProvider(), new RecordingAutoLogoutInteractionService());

            try
            {
                Assert.Same(viewModel, window.DataContext);
                Assert.Equal(LocalizedText.Get("MainWindow.Title"), viewModel.Title);
                Assert.Equal(LocalizedText.Get("MainWindowView.BrandTitle"), viewModel.BrandTitle);

                var trayIcon = window.FindName("TrayNotifyIcon") as NotifyIcon;
                Assert.NotNull(trayIcon);
                Assert.Equal(System.Windows.Visibility.Collapsed, trayIcon!.Visibility);

                var trayContent = window.FindName("TrayContextContentControl") as MainWindowTrayContentControl;
                Assert.NotNull(trayContent);
            }
            finally
            {
                window.Close();
            }
        }, ensureApplicationResources: true);
    }

    [Fact]
    public void MinimizeWindow_ShouldNotAutomaticallySwitchToTray()
    {
        using var cultureScope = new TestCultureScope("en-US");

        WpfTestHost.Run(() =>
        {
            var autoLogoutInteractionService = new RecordingAutoLogoutInteractionService();
            var window = CreateWindow(autoLogoutInteractionService);

            try
            {
                window.Show();
                window.WindowState = WindowState.Minimized;
                WpfTestHost.DrainDispatcher();

                var trayIcon = Assert.IsType<NotifyIcon>(window.FindName("TrayNotifyIcon"));
                Assert.Equal(System.Windows.Visibility.Collapsed, trayIcon.Visibility);
                Assert.True(window.ShowInTaskbar);
                Assert.False(GetPrivateField<bool>(window, "_isInTray"));
                Assert.False(GetPrivateField<bool>(window, "_hasShownFirstTrayBalloonTip"));

                window.WindowState = WindowState.Normal;
                WpfTestHost.DrainDispatcher();

                Assert.Equal(System.Windows.Visibility.Collapsed, trayIcon.Visibility);
                Assert.False(GetPrivateField<bool>(window, "_isInTray"));
            }
            finally
            {
                window.Close();
            }
        }, ensureApplicationResources: true);
    }

    [Fact]
    public void SendToTray_FromCloseAction_ShouldShowTrayIconAndFirstBalloonFlag()
    {
        using var cultureScope = new TestCultureScope("en-US");

        WpfTestHost.Run(() =>
        {
            var autoLogoutInteractionService = new RecordingAutoLogoutInteractionService();
            var window = CreateWindow(autoLogoutInteractionService);

            try
            {
                window.Show();
                InvokePrivate(window, "SendToTray", true, true);
                WpfTestHost.DrainDispatcher();

                var trayIcon = Assert.IsType<NotifyIcon>(window.FindName("TrayNotifyIcon"));
                Assert.Equal(System.Windows.Visibility.Visible, trayIcon.Visibility);
                Assert.False(window.ShowInTaskbar);
                Assert.False(window.IsVisible);
                Assert.True(GetPrivateField<bool>(window, "_isInTray"));
                Assert.True(GetPrivateField<bool>(window, "_hasShownFirstTrayBalloonTip"));
            }
            finally
            {
                window.Close();
            }
        }, ensureApplicationResources: true);
    }

    [Fact]
    public void RestoreFromTray_ShouldShowWindowAndHideTrayIcon()
    {
        using var cultureScope = new TestCultureScope("en-US");

        WpfTestHost.Run(() =>
        {
            var autoLogoutInteractionService = new RecordingAutoLogoutInteractionService();
            var window = CreateWindow(autoLogoutInteractionService);

            try
            {
                window.Show();
                InvokePrivate(window, "SendToTray", true, false);
                InvokePrivate(window, "RestoreFromTray");
                WpfTestHost.DrainDispatcher();

                var trayIcon = Assert.IsType<NotifyIcon>(window.FindName("TrayNotifyIcon"));
                Assert.True(window.IsVisible);
                Assert.True(window.ShowInTaskbar);
                Assert.Equal(WindowState.Normal, window.WindowState);
                Assert.Equal(System.Windows.Visibility.Collapsed, trayIcon.Visibility);
                Assert.False(GetPrivateField<bool>(window, "_isInTray"));
            }
            finally
            {
                window.Close();
            }
        }, ensureApplicationResources: true);
    }

    [Fact]
    public void RestoreFromTray_ShouldRestoreNormalWindowBounds()
    {
        using var cultureScope = new TestCultureScope("en-US");

        WpfTestHost.Run(() =>
        {
            var autoLogoutInteractionService = new RecordingAutoLogoutInteractionService();
            var window = CreateWindow(autoLogoutInteractionService);

            try
            {
                window.Left = 120;
                window.Top = 140;
                window.Width = 980;
                window.Height = 620;
                window.Show();
                WpfTestHost.DrainDispatcher();

                InvokePrivate(window, "SendToTray", true, false);
                InvokePrivate(window, "RestoreFromTray");
                WpfTestHost.DrainDispatcher();

                Assert.Equal(WindowState.Normal, window.WindowState);
                Assert.Equal(120, window.Left, 1);
                Assert.Equal(140, window.Top, 1);
                Assert.Equal(980, window.Width, 1);
                Assert.Equal(620, window.Height, 1);
            }
            finally
            {
                window.Close();
            }
        }, ensureApplicationResources: true);
    }

    [Fact]
    public void RestoreFromTray_ShouldRestoreMaximizedWindowState()
    {
        using var cultureScope = new TestCultureScope("en-US");

        WpfTestHost.Run(() =>
        {
            var autoLogoutInteractionService = new RecordingAutoLogoutInteractionService();
            var window = CreateWindow(autoLogoutInteractionService);

            try
            {
                window.Left = 160;
                window.Top = 180;
                window.Width = 900;
                window.Height = 600;
                window.Show();
                window.WindowState = WindowState.Maximized;
                WpfTestHost.DrainDispatcher();

                InvokePrivate(window, "SendToTray", true, false);
                InvokePrivate(window, "RestoreFromTray");
                WpfTestHost.DrainDispatcher();

                Assert.Equal(WindowState.Maximized, window.WindowState);
            }
            finally
            {
                window.Close();
            }
        }, ensureApplicationResources: true);
    }

    [Fact]
    public void EnsureUserCanExit_WhenNotLoggedIn_ShouldReturnFalseAndShowWarning()
    {
        using var cultureScope = new TestCultureScope("en-US");

        WpfTestHost.Run(() =>
        {
            var autoLogoutInteractionService = new RecordingAutoLogoutInteractionService(MessageBoxResult.OK);
            var window = CreateWindow(autoLogoutInteractionService);

            try
            {
                var canExit = InvokePrivate<bool>(window, "EnsureUserCanExit");

                Assert.False(canExit);
                Assert.Equal(1, autoLogoutInteractionService.RunModalCallCount);
            }
            finally
            {
                window.Close();
            }
        }, ensureApplicationResources: true);
    }

    [Fact]
    public void ConfirmTrayExit_WhenUserCancels_ShouldReturnFalse()
    {
        using var cultureScope = new TestCultureScope("en-US");

        WpfTestHost.Run(() =>
        {
            var autoLogoutInteractionService = new RecordingAutoLogoutInteractionService(MessageBoxResult.No);
            var window = CreateWindow(autoLogoutInteractionService, isLoggedIn: true);

            try
            {
                var confirmed = InvokePrivate<bool>(window, "ConfirmTrayExit");

                Assert.False(confirmed);
                Assert.Equal(1, autoLogoutInteractionService.RunModalCallCount);
            }
            finally
            {
                window.Close();
            }
        }, ensureApplicationResources: true);
    }

    private static MainWindow CreateWindow(RecordingAutoLogoutInteractionService autoLogoutInteractionService, bool isLoggedIn = false)
    {
        var currentUserAccessor = new CurrentUserAccessor();
        var loginService = new StubLoginService();

        if (isLoggedIn)
        {
            var user = new MhrUser
            {
                WorkId = "E10001",
                AccessLevel = 2
            };
            currentUserAccessor.SetCurrentUser(user);
            loginService.SetCurrentUser(user);
        }

        var viewModel = new MainWindowViewModel(
            new StubLocalizationService(),
            new StubServiceProvider(),
            loginService,
            new StubAppSettingsService(),
            new StubUserConfigService(),
            new StubClientAppInfoService(),
            new UiBusyService(TimeSpan.Zero),
            new StubPlcStartupConnectionService(),
            new LoginSessionStateMachine(currentUserAccessor, loginService),
            new StubUiDispatcher(),
            new StubAppStartupCoordinator());

        return new MainWindow(viewModel, new StubServiceProvider(), autoLogoutInteractionService);
    }

    private static void InvokePrivate(object target, string methodName, params object?[] args)
    {
        _ = InvokePrivate<object?>(target, methodName, args);
    }

    private static T InvokePrivate<T>(object target, string methodName, params object?[] args)
    {
        var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var result = method!.Invoke(target, args);
        return result is null ? default! : (T)result;
    }

    private static T GetPrivateField<T>(object target, string fieldName)
    {
        var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return (T)field!.GetValue(target)!;
    }

    private sealed class StubLocalizationService : ILocalizationService
    {
        public string this[string name] => LocalizedText.Get(name);

        public LocalizationCatalog Catalog { get; } = new(LocalizedText.Get);

        public CultureInfo CurrentCulture => CultureInfo.CurrentUICulture;

        public ValueTask InitializeAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public ValueTask SetCultureAsync(string cultureName, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    }

    private sealed class StubLoginService : ILoginService
    {
        private MhrUser? _currentUser;

        public Task<MhrUser?> LoginAsync(string authId, string factory, string resourceId, bool isIdCard, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public MhrUser? GetCurrentUser() => _currentUser;

        public void SetCurrentUser(MhrUser? user)
        {
            _currentUser = user;
        }

        public ValueTask LogoutAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    }

    private sealed class StubAppSettingsService : IAppSettingsService
    {
        public event EventHandler<AppSettings>? SettingsSaved;

        public ValueTask<AppSettings> GetAsync(CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(new AppSettings
            {
                IsSetClientAppInfo = true,
                AutoLogoutCountdownSeconds = 360
            });
        }

        public ValueTask SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
        {
            SettingsSaved?.Invoke(this, settings);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class StubClientAppInfoService : IClientAppInfoService
    {
        public Task<ClientAppInfoModel> GetAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ClientAppInfoModel
            {
                ProcedureCode = "模切分条"
            });
        }

        public Task<ClientAppInfoModel> SaveAsync(ClientAppInfoSaveRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class StubUserConfigService : IUserConfigService
    {
        public ValueTask<UserConfig> GetAsync(CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(new UserConfig
            {
                Language = "en-US"
            });
        }

        public ValueTask SaveAsync(UserConfig config, CancellationToken cancellationToken = default)
        {
            return ValueTask.CompletedTask;
        }
    }

    private sealed class StubPlcStartupConnectionService : IPlcStartupConnectionService
    {
        public Task<PlcStartupConnectionResult> EnsureConnectedAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(PlcStartupConnectionResult.Connected());
        }
    }

    private sealed class StubUiDispatcher : IUiDispatcher
    {
        public void Run(Action action) => action();

        public Task RunAsync(Action action, DispatcherPriority priority = DispatcherPriority.Normal)
        {
            action();
            return Task.CompletedTask;
        }

        public Task RenderAsync() => Task.CompletedTask;
    }

    private sealed class StubAppStartupCoordinator : IAppStartupCoordinator
    {
        public Task EnsureInitializedAsync(Func<string, Task>? reportLoadingAsync = null, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class StubServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType)
        {
            if (typeof(FrameworkElement).IsAssignableFrom(serviceType))
            {
                return new Border();
            }

            return null;
        }
    }

    private sealed class StubCurrentUserAccessor : ICurrentUserAccessor
    {
        public MhrUser? CurrentUser => null;

        public string? UserId => null;

        public event EventHandler? CurrentUserChanged;

        public void SetCurrentUser(MhrUser? user)
        {
            CurrentUserChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Clear()
        {
            CurrentUserChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private sealed class RecordingAutoLogoutInteractionService : IAutoLogoutInteractionService
    {
        private readonly Queue<object?> _results;

        public RecordingAutoLogoutInteractionService(params object?[] results)
        {
            _results = new Queue<object?>(results);
        }

        public int RunModalCallCount { get; private set; }

        public void NotifyActivity()
        {
        }

        public TResult RunModal<TResult>(Func<TResult> interaction)
        {
            RunModalCallCount++;

            if (_results.Count > 0)
            {
                return (TResult)_results.Dequeue()!;
            }

            return interaction();
        }
    }
}