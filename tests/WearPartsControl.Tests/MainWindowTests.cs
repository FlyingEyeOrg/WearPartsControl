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
using WearPartsControl.ApplicationServices.Shell;
using WearPartsControl.ApplicationServices.Startup;
using WearPartsControl.Domain.Entities;
using WearPartsControl.Domain.Repositories;
using WearPartsControl.Infrastructure.EntityFrameworkCore;
using WearPartsControl.UserControls;
using WearPartsControl.ViewModels;
using WearPartsControl.Views;
using Xunit;

namespace WearPartsControl.Tests;

[Collection(NavigationTabControlTestCollection.Name)]
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
                new MainWindowNavigationService(),
                new StubMainWindowContentFactory(),
                loginService,
                new StubAppSettingsService(),
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
                window.Height = 680;
                window.Show();
                WpfTestHost.DrainDispatcher();

                InvokePrivate(window, "SendToTray", true, false);
                InvokePrivate(window, "RestoreFromTray");
                WpfTestHost.DrainDispatcher();

                Assert.Equal(WindowState.Normal, window.WindowState);
                Assert.Equal(120, window.Left, 1);
                Assert.Equal(140, window.Top, 1);
                Assert.Equal(980, window.Width, 1);
                Assert.Equal(680, window.Height, 1);
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
    public void EnsureUserCanExit_WhenClientAppInfoMissing_ShouldReturnTrueWithoutWarning()
    {
        using var cultureScope = new TestCultureScope("en-US");

        WpfTestHost.Run(() =>
        {
            var autoLogoutInteractionService = new RecordingAutoLogoutInteractionService(MessageBoxResult.OK);
            var window = CreateWindow(autoLogoutInteractionService, isClientAppInfoConfigured: false);

            try
            {
                var canExit = InvokePrivate<bool>(window, "EnsureUserCanExit");

                Assert.True(canExit);
                Assert.Equal(0, autoLogoutInteractionService.RunModalCallCount);
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

    [Fact]
    public void ExitFromTrayAsync_WhenNotLoggedInAndLoginCanceled_ShouldNotExit()
    {
        using var cultureScope = new TestCultureScope("en-US");

        WpfTestHost.Run(() =>
        {
            var autoLogoutInteractionService = new RecordingAutoLogoutInteractionService();
            var window = CreateWindow(autoLogoutInteractionService, showLoginDialog: () => false);

            try
            {
                window.Show();
                InvokePrivate(window, "SendToTray", true, false);

                InvokePrivate<Task>(window, "ExitFromTrayAsync").GetAwaiter().GetResult();

                var trayIcon = Assert.IsType<NotifyIcon>(window.FindName("TrayNotifyIcon"));
                Assert.Equal(System.Windows.Visibility.Visible, trayIcon.Visibility);
                Assert.True(GetPrivateField<bool>(window, "_isInTray"));
                Assert.False(GetPrivateField<bool>(window, "_isExitRequested"));
            }
            finally
            {
                window.Close();
            }
        }, ensureApplicationResources: true);
    }

    [Fact]
    public void ExitFromTrayAsync_WhenNotLoggedInAndLoginSucceeds_ShouldExit()
    {
        using var cultureScope = new TestCultureScope("en-US");

        WpfTestHost.Run(() =>
        {
            var autoLogoutInteractionService = new RecordingAutoLogoutInteractionService();
            var window = CreateWindow(autoLogoutInteractionService, showLoginDialog: () => true);

            try
            {
                window.Show();
                InvokePrivate(window, "SendToTray", true, false);

                InvokePrivate<Task>(window, "ExitFromTrayAsync").GetAwaiter().GetResult();

                Assert.True(GetPrivateField<bool>(window, "_isExitRequested"));
            }
            finally
            {
                if (window.IsLoaded)
                {
                    window.Close();
                }
            }
        }, ensureApplicationResources: true);
    }

    [Fact]
    public void ExitFromTrayAsync_WhenClientAppInfoMissing_ShouldExitWithoutLogin()
    {
        using var cultureScope = new TestCultureScope("en-US");

        WpfTestHost.Run(() =>
        {
            var autoLogoutInteractionService = new RecordingAutoLogoutInteractionService();
            var loginPromptCount = 0;
            var window = CreateWindow(
                autoLogoutInteractionService,
                isClientAppInfoConfigured: false,
                showLoginDialog: () =>
                {
                    loginPromptCount++;
                    return false;
                });

            try
            {
                window.Show();
                InvokePrivate(window, "SendToTray", true, false);

                InvokePrivate<Task>(window, "ExitFromTrayAsync").GetAwaiter().GetResult();

                Assert.True(GetPrivateField<bool>(window, "_isExitRequested"));
                Assert.Equal(0, loginPromptCount);
                Assert.Equal(0, autoLogoutInteractionService.RunModalCallCount);
            }
            finally
            {
                if (window.IsLoaded)
                {
                    window.Close();
                }
            }
        }, ensureApplicationResources: true);
    }

    [Fact]
    public void OnMainWindowActivated_WhenStillInTray_ShouldKeepTrayIconVisible()
    {
        using var cultureScope = new TestCultureScope("en-US");

        WpfTestHost.Run(() =>
        {
            var autoLogoutInteractionService = new RecordingAutoLogoutInteractionService(MessageBoxResult.OK);
            var window = CreateWindow(autoLogoutInteractionService);

            try
            {
                window.Show();
                InvokePrivate(window, "SendToTray", true, false);
                WpfTestHost.DrainDispatcher();

                var trayIcon = Assert.IsType<NotifyIcon>(window.FindName("TrayNotifyIcon"));
                Assert.Equal(System.Windows.Visibility.Visible, trayIcon.Visibility);
                Assert.True(GetPrivateField<bool>(window, "_isInTray"));

                _ = InvokePrivate<bool>(window, "EnsureUserCanExit");
                InvokePrivate(window, "OnMainWindowActivated", null, EventArgs.Empty);

                Assert.Equal(System.Windows.Visibility.Visible, trayIcon.Visibility);
                Assert.True(GetPrivateField<bool>(window, "_isInTray"));
            }
            finally
            {
                window.Close();
            }
        }, ensureApplicationResources: true);
    }

    [Fact]
    public void ShowLoginDialog_WhenWindowVisible_ShouldUseMainWindowAsOwner()
    {
        using var cultureScope = new TestCultureScope("en-US");

        WpfTestHost.Run(() =>
        {
            var loginWindow = CreateLoginWindow();
            var autoLogoutInteractionService = new RecordingAutoLogoutInteractionService(false);
            var window = CreateWindow(autoLogoutInteractionService, serviceProvider: new LoginWindowServiceProvider(loginWindow));

            try
            {
                window.Show();
                WpfTestHost.DrainDispatcher();

                var result = InvokePrivate<bool>(window, "ShowLoginDialog");

                Assert.False(result);
                Assert.Same(window, loginWindow.Owner);
                Assert.Equal(WindowStartupLocation.CenterOwner, loginWindow.WindowStartupLocation);
            }
            finally
            {
                if (loginWindow.IsLoaded)
                {
                    loginWindow.Close();
                }

                window.Close();
            }
        }, ensureApplicationResources: true);
    }

    [Fact]
    public void ShowLoginDialog_WhenWindowInTray_ShouldNotUseHiddenMainWindowAsOwner()
    {
        using var cultureScope = new TestCultureScope("en-US");

        WpfTestHost.Run(() =>
        {
            var loginWindow = CreateLoginWindow();
            var autoLogoutInteractionService = new RecordingAutoLogoutInteractionService(false);
            var window = CreateWindow(autoLogoutInteractionService, serviceProvider: new LoginWindowServiceProvider(loginWindow));

            try
            {
                window.Show();
                InvokePrivate(window, "SendToTray", true, false);
                WpfTestHost.DrainDispatcher();

                var result = InvokePrivate<bool>(window, "ShowLoginDialog");

                Assert.False(result);
                Assert.Null(loginWindow.Owner);
                Assert.Equal(WindowStartupLocation.CenterScreen, loginWindow.WindowStartupLocation);
            }
            finally
            {
                if (loginWindow.IsLoaded)
                {
                    loginWindow.Close();
                }

                if (window.IsLoaded)
                {
                    window.Close();
                }
            }
        }, ensureApplicationResources: true);
    }

    private static MainWindow CreateWindow(
        RecordingAutoLogoutInteractionService autoLogoutInteractionService,
        bool isLoggedIn = false,
        bool isClientAppInfoConfigured = true,
        Func<bool>? showLoginDialog = null,
        IServiceProvider? serviceProvider = null)
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
            new MainWindowNavigationService(),
            new StubMainWindowContentFactory(),
            loginService,
            new StubAppSettingsService(),
            new StubClientAppInfoService(),
            new UiBusyService(TimeSpan.Zero),
            new StubPlcStartupConnectionService(),
            new LoginSessionStateMachine(currentUserAccessor, loginService),
            new StubUiDispatcher(),
            new StubAppStartupCoordinator());
        SetPrivateProperty(viewModel, nameof(MainWindowViewModel.IsClientAppInfoConfigured), isClientAppInfoConfigured);

        return new MainWindow(viewModel, serviceProvider ?? new StubServiceProvider(), autoLogoutInteractionService, showLoginDialog);
    }

    private static LoginWindow CreateLoginWindow()
    {
        return new LoginWindow(new LoginWindowViewModel(
            new StubLoginWindowService(),
            new StubLoginWindowClientAppConfigurationRepository(),
            new StubLoginWindowAppSettingsService(),
            new StubUiDispatcher()));
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

    private static void SetPrivateProperty<T>(object target, string propertyName, T value)
    {
        var property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(property);
        property!.SetValue(target, value);
    }

    private sealed class StubLocalizationService : ILocalizationService
    {
        public string this[string name] => LocalizedText.Get(name);

        public LocalizationCatalog Catalog { get; } = new(LocalizedText.Get);

        public CultureInfo CurrentCulture => CultureInfo.CurrentUICulture;

        public ValueTask InitializeAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public ValueTask SetCultureAsync(string cultureName, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    }

    private sealed class StubMainWindowContentFactory : IMainWindowContentFactory
    {
        public object Create(Type contentType)
        {
            return new ContentControl();
        }
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

    private sealed class LoginWindowServiceProvider : IServiceProvider
    {
        private readonly LoginWindow _loginWindow;

        public LoginWindowServiceProvider(LoginWindow loginWindow)
        {
            _loginWindow = loginWindow;
        }

        public object? GetService(Type serviceType)
        {
            if (serviceType == typeof(LoginWindow))
            {
                return _loginWindow;
            }

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

    private sealed class StubLoginWindowService : ILoginService
    {
        public Task<MhrUser?> LoginAsync(string authId, string factory, string resourceId, bool isIdCard, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<MhrUser?>(new MhrUser
            {
                WorkId = "WORK-01",
                AccessLevel = 1,
                CardId = authId
            });
        }

        public MhrUser? GetCurrentUser() => null;

        public ValueTask LogoutAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    }

    private sealed class StubLoginWindowAppSettingsService : IAppSettingsService
    {
        public event EventHandler<AppSettings>? SettingsSaved;

        public ValueTask<AppSettings> GetAsync(CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(new AppSettings
            {
                ResourceNumber = "RES-001",
                LoginInputMaxIntervalMilliseconds = 80
            });
        }

        public ValueTask SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
        {
            SettingsSaved?.Invoke(this, settings);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class StubLoginWindowClientAppConfigurationRepository : IClientAppConfigurationRepository
    {
        public IUnitOfWork UnitOfWork => throw new NotSupportedException();

        public Task<ClientAppConfigurationEntity?> GetByResourceNumberAsync(string resourceNumber, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<ClientAppConfigurationEntity?>(new ClientAppConfigurationEntity
            {
                SiteCode = "SITE-01",
                FactoryCode = "FACTORY-01",
                AreaCode = "AREA-01",
                ProcedureCode = "PROC-01",
                EquipmentCode = "EQ-01",
                ResourceNumber = resourceNumber,
                PlcProtocolType = "SiemensS7",
                PlcIpAddress = "127.0.0.1",
                PlcPort = 102
            });
        }

        public Task<ClientAppConfigurationEntity?> GetForUpdateByResourceNumberAsync(string resourceNumber, CancellationToken cancellationToken = default)
        {
            return GetByResourceNumberAsync(resourceNumber, cancellationToken);
        }

        public Task<bool> ExistsByResourceNumberAsync(string resourceNumber, Guid? excludeId = null, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyList<ClientAppConfigurationEntity>> ListAsync(CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<ClientAppConfigurationEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<ClientAppConfigurationEntity?> GetForUpdateByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task AddAsync(ClientAppConfigurationEntity entity, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task UpdateAsync(ClientAppConfigurationEntity entity, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task SoftDeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}