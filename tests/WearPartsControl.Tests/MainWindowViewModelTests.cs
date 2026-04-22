using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Windows.Threading;
using WearPartsControl.ApplicationServices;
using WearPartsControl.ApplicationServices.AppSettings;
using WearPartsControl.ApplicationServices.Localization;
using WearPartsControl.ApplicationServices.Localization.Generated;
using WearPartsControl.ApplicationServices.LoginService;
using WearPartsControl.ApplicationServices.PlcService;
using WearPartsControl.ApplicationServices.Startup;
using WearPartsControl.UserControls;
using WearPartsControl.ViewModels;
using Xunit;

namespace WearPartsControl.Tests;

[Collection(LocalizationSensitiveTestCollection.Name)]
public sealed class MainWindowViewModelTests : IDisposable
{
    private readonly TestCultureScope _cultureScope = new("zh-CN");

    [Fact]
    public async Task CurrentUserChanged_ShouldRefreshLoginStatus()
    {
        var appSettingsService = new StubAppSettingsService
        {
            Current = new AppSettings
            {
                IsSetClientAppInfo = true,
                AutoLogoutCountdownSeconds = 360
            }
        };
        var accessor = new CurrentUserAccessor();
        var loginService = new StubLoginService();
        var viewModel = CreateViewModel(accessor, loginService, appSettingsService, new UiBusyService(), new StubPlcStartupConnectionService());

        await viewModel.InitializeAsync();

        Assert.True(viewModel.IsClientAppInfoConfigured);
        Assert.Equal(LocalizedText.Get("ViewModels.MainWindowVm.CurrentUserWorkIdEmpty"), viewModel.CurrentUserWorkIdText);
        Assert.Equal(LocalizedText.Get("ViewModels.MainWindowVm.CurrentUserAccessLevelEmpty"), viewModel.CurrentUserAccessLevelText);
        Assert.False(viewModel.IsLoggedIn);

        accessor.SetCurrentUser(new MhrUser
        {
            CardId = "CARD-01",
            WorkId = "WORK-02",
            AccessLevel = 3
        });

        Assert.Equal(LocalizedText.Format("ViewModels.MainWindowVm.CurrentUserWorkId", "WORK-02"), viewModel.CurrentUserWorkIdText);
        Assert.Equal(LocalizedText.Format("ViewModels.MainWindowVm.CurrentUserAccessLevelCountdown", 3, "06:00"), viewModel.CurrentUserAccessLevelText);
        Assert.True(viewModel.IsLoggedIn);
        Assert.False(viewModel.ShowLoginButton);
        Assert.True(viewModel.ShowLogoutButton);
        Assert.True(viewModel.LogoutCommand.CanExecute(null));
    }

    [Fact]
    public async Task InitialState_WhenNoUser_ShouldShowLoginAndDisableLogout()
    {
        var appSettingsService = new StubAppSettingsService
        {
            Current = new AppSettings
            {
                IsSetClientAppInfo = true,
                AutoLogoutCountdownSeconds = 360
            }
        };

        var viewModel = CreateViewModel(new CurrentUserAccessor(), new StubLoginService(), appSettingsService, new UiBusyService(), new StubPlcStartupConnectionService());

        await viewModel.InitializeAsync();

        Assert.False(viewModel.IsLoggedIn);
        Assert.True(viewModel.ShowLoginButton);
        Assert.False(viewModel.ShowLogoutButton);
        Assert.False(viewModel.LogoutCommand.CanExecute(null));
    }

    [Fact]
    public async Task AppSettingsSaved_ShouldRefreshTabs()
    {
        var appSettingsService = new StubAppSettingsService
        {
            Current = new AppSettings
            {
                IsSetClientAppInfo = false,
                AutoLogoutCountdownSeconds = 360
            }
        };

        var viewModel = CreateViewModel(new CurrentUserAccessor(), new StubLoginService(), appSettingsService, new UiBusyService(), new StubPlcStartupConnectionService());

        Assert.False(viewModel.IsClientAppInfoConfigured);

        await viewModel.InitializeAsync();

        Assert.Single(viewModel.Tabs);
        Assert.False(viewModel.IsClientAppInfoConfigured);

        await appSettingsService.SaveAsync(new AppSettings
        {
            IsSetClientAppInfo = true,
            ResourceNumber = "RES-01"
        });

        Assert.Equal(5, viewModel.Tabs.Count());
        Assert.True(viewModel.IsClientAppInfoConfigured);
    }

    [Fact]
    public async Task CurrentUserChanged_ShouldAutoLogoutAfterCountdownElapsed()
    {
        var appSettingsService = new StubAppSettingsService
        {
            Current = new AppSettings
            {
                IsSetClientAppInfo = true,
                AutoLogoutCountdownSeconds = 2
            }
        };
        var accessor = new CurrentUserAccessor();
        var delaySignals = new Queue<TaskCompletionSource<bool>>();
        var loginService = new StubLoginService(accessor);
        var viewModel = CreateViewModel(
            accessor,
            loginService,
            appSettingsService,
            new UiBusyService(),
            new StubPlcStartupConnectionService(),
            (delay, cancellationToken) =>
            {
                var signal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                delaySignals.Enqueue(signal);
                cancellationToken.Register(() => signal.TrySetCanceled(cancellationToken));
                return signal.Task;
            });

        await viewModel.InitializeAsync();

        accessor.SetCurrentUser(new MhrUser
        {
            CardId = "CARD-01",
            WorkId = "WORK-02",
            AccessLevel = 3
        });

        Assert.Equal(LocalizedText.Format("ViewModels.MainWindowVm.CurrentUserAccessLevelCountdown", 3, "00:02"), viewModel.CurrentUserAccessLevelText);

        delaySignals.Dequeue().SetResult(true);
        await WaitUntilAsync(() => viewModel.CurrentUserAccessLevelText == LocalizedText.Format("ViewModels.MainWindowVm.CurrentUserAccessLevelCountdown", 3, "00:01"));

        delaySignals.Dequeue().SetResult(true);
        await WaitUntilAsync(() => !viewModel.IsLoggedIn);

        Assert.Equal(1, loginService.LogoutCount);
        Assert.Equal(LocalizedText.Get("ViewModels.MainWindowVm.CurrentUserAccessLevelEmpty"), viewModel.CurrentUserAccessLevelText);
    }

    [Fact]
    public async Task InitializeAsync_WhenClientAppConfigured_ShouldConnectWithBusyState()
    {
        var appSettingsService = new StubAppSettingsService
        {
            Current = new AppSettings
            {
                IsSetClientAppInfo = true,
                AutoLogoutCountdownSeconds = 360
            }
        };
        var startupConnectionService = new StubPlcStartupConnectionService
        {
            PendingResult = new TaskCompletionSource<PlcStartupConnectionResult>(TaskCreationOptions.RunContinuationsAsynchronously)
        };
        var viewModel = CreateViewModel(new CurrentUserAccessor(), new StubLoginService(), appSettingsService, new UiBusyService(TimeSpan.Zero), startupConnectionService);

        var initializeTask = viewModel.InitializeAsync();

    await WaitUntilAsync(() => startupConnectionService.CallCount == 1);
    await WaitUntilAsync(() => viewModel.IsBusy);

        Assert.True(viewModel.IsBusy);
        Assert.False(viewModel.IsNotBusy);
        Assert.Equal(LocalizedText.Get("Services.PlcStartupConnection.Connecting"), viewModel.LoadingText);
        Assert.True(viewModel.HasLoadingText);
        Assert.Equal(1, startupConnectionService.CallCount);

        startupConnectionService.PendingResult.SetResult(PlcStartupConnectionResult.Connected());
        await initializeTask;

        Assert.False(viewModel.IsBusy);
        Assert.True(viewModel.IsNotBusy);
        Assert.Equal(string.Empty, viewModel.LoadingText);
        Assert.False(viewModel.HasLoadingText);
    }

    [Fact]
    public async Task InitializeAsync_ShouldOnlyRunOnce()
    {
        var appSettingsService = new StubAppSettingsService
        {
            Current = new AppSettings
            {
                IsSetClientAppInfo = true,
                AutoLogoutCountdownSeconds = 360
            }
        };
        var startupConnectionService = new StubPlcStartupConnectionService();
        var viewModel = CreateViewModel(new CurrentUserAccessor(), new StubLoginService(), appSettingsService, new UiBusyService(TimeSpan.Zero), startupConnectionService);

        await viewModel.InitializeAsync();
        await viewModel.InitializeAsync();

        Assert.Equal(1, startupConnectionService.CallCount);
    }

    [Fact]
    public async Task Constructor_ShouldNotReadAppSettingsSynchronously()
    {
        var appSettingsService = new StubAppSettingsService
        {
            Current = new AppSettings
            {
                IsSetClientAppInfo = true,
                AutoLogoutCountdownSeconds = 360
            }
        };

        var viewModel = CreateViewModel(new CurrentUserAccessor(), new StubLoginService(), appSettingsService, new UiBusyService(TimeSpan.Zero), new StubPlcStartupConnectionService());

        Assert.Equal(0, appSettingsService.GetAsyncCallCount);

        await viewModel.InitializeAsync();

        Assert.Equal(1, appSettingsService.GetAsyncCallCount);
    }

    public void Dispose()
    {
        _cultureScope.Dispose();
    }

    private sealed class StubLoginService : ILoginService
    {
        private readonly CurrentUserAccessor? _currentUserAccessor;

        public StubLoginService(CurrentUserAccessor? currentUserAccessor = null)
        {
            _currentUserAccessor = currentUserAccessor;
        }

        public int LogoutCount { get; private set; }

        public Task<MhrUser?> LoginAsync(string authId, string factory, string resourceId, bool isIdCard, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public MhrUser? GetCurrentUser() => null;

        public ValueTask LogoutAsync(CancellationToken cancellationToken = default)
        {
            LogoutCount++;
            _currentUserAccessor?.Clear();
            return ValueTask.CompletedTask;
        }
    }

    private sealed class StubAppSettingsService : IAppSettingsService
    {
        public AppSettings Current { get; set; } = new();

        public int GetAsyncCallCount { get; private set; }

        public event EventHandler<AppSettings>? SettingsSaved;

        public ValueTask<AppSettings> GetAsync(CancellationToken cancellationToken = default)
        {
            GetAsyncCallCount++;
            return ValueTask.FromResult(new AppSettings
            {
                ResourceNumber = Current.ResourceNumber,
                LoginInputMaxIntervalMilliseconds = Current.LoginInputMaxIntervalMilliseconds,
                AutoLogoutCountdownSeconds = Current.AutoLogoutCountdownSeconds,
                IsSetClientAppInfo = Current.IsSetClientAppInfo
            });
        }

        public ValueTask SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
        {
            Current = settings;
            SettingsSaved?.Invoke(this, settings);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class StubPlcStartupConnectionService : IPlcStartupConnectionService
    {
        public int CallCount { get; private set; }

        public PlcStartupConnectionResult Result { get; set; } = PlcStartupConnectionResult.Connected();

        public TaskCompletionSource<PlcStartupConnectionResult>? PendingResult { get; set; }

        public Task<PlcStartupConnectionResult> EnsureConnectedAsync(CancellationToken cancellationToken = default)
        {
            CallCount++;

            if (PendingResult is not null)
            {
                cancellationToken.Register(() => PendingResult.TrySetCanceled(cancellationToken));
                return PendingResult.Task;
            }

            return Task.FromResult(Result);
        }
    }

    private static async Task WaitUntilAsync(Func<bool> predicate)
    {
        for (var index = 0; index < 20; index++)
        {
            if (predicate())
            {
                return;
            }

            await Task.Delay(10);
        }

        Assert.True(predicate());
    }

    private static MainWindowViewModel CreateViewModel(
        CurrentUserAccessor accessor,
        StubLoginService loginService,
        StubAppSettingsService appSettingsService,
        IUiBusyService uiBusyService,
        IPlcStartupConnectionService startupConnectionService,
        Func<TimeSpan, CancellationToken, Task>? delayAsync = null)
    {
        var stateMachine = new LoginSessionStateMachine(accessor, loginService, delayAsync);
        return new MainWindowViewModel(
            new StubLocalizationService(),
            new StubServiceProvider(),
            loginService,
            appSettingsService,
            uiBusyService,
            startupConnectionService,
            stateMachine,
                new StubUiDispatcher(),
                new StubAppStartupCoordinator());
    }

            private sealed class StubAppStartupCoordinator : IAppStartupCoordinator
            {
            public Task EnsureInitializedAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
            }

    private sealed class StubLocalizationService : ILocalizationService
    {
        public string this[string name] => name;

        public LocalizationCatalog Catalog { get; } = new(static key => key);

        public ValueTask InitializeAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public ValueTask SetCultureAsync(string cultureName, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public CultureInfo CurrentCulture { get; } = CultureInfo.InvariantCulture;
    }

    private sealed class StubServiceProvider : IServiceProvider
    {
        private readonly Dictionary<Type, object> _services = new()
        {
            [typeof(ReplacePartUserControl)] = RuntimeHelpers.GetUninitializedObject(typeof(ReplacePartUserControl)),
            [typeof(ClientAppInfoUserControl)] = RuntimeHelpers.GetUninitializedObject(typeof(ClientAppInfoUserControl))
        };

        public object? GetService(Type serviceType)
        {
            return _services.TryGetValue(serviceType, out var service)
                ? service
                : null;
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
}