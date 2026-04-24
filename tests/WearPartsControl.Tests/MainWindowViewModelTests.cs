using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Windows.Threading;
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

        Assert.Equal(6, viewModel.Tabs.Count());
        Assert.True(viewModel.IsClientAppInfoConfigured);
    }

    [Fact]
    public async Task InitializeAsync_WhenProcedureIsNotDieCutSlitting_ShouldHideToolChangeTab()
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
        var serviceProvider = new StubServiceProvider();
        var clientAppInfoService = new StubClientAppInfoService
        {
            Model = new ClientAppInfoModel
            {
                ResourceNumber = "RES-01",
                ProcedureCode = "涂布"
            }
        };
        var viewModel = CreateViewModel(accessor, new StubLoginService(), appSettingsService, new UiBusyService(), new StubPlcStartupConnectionService(), serviceProvider: serviceProvider, clientAppInfoService: clientAppInfoService);

        await viewModel.InitializeAsync();
        accessor.SetCurrentUser(new MhrUser
        {
            CardId = "CARD-01",
            WorkId = "WORK-02",
            AccessLevel = 3
        });

        Assert.Equal(5, viewModel.Tabs.Count());

        viewModel.TabChangedCommand.Execute(3);

        Assert.Equal(0, serviceProvider.GetResolveCount<ToolChangeManagementUserControl>());
        Assert.Equal(1, serviceProvider.GetResolveCount<PartUpdateRecordUserControl>());
        Assert.IsType<PartUpdateRecordUserControl>(viewModel.SelectedContent);
    }

    [Fact]
    public async Task InitializeAsync_WhenProcedureIsDieCutSlitting_ShouldShowToolChangeTab()
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
        var serviceProvider = new StubServiceProvider();
        var clientAppInfoService = new StubClientAppInfoService
        {
            Model = new ClientAppInfoModel
            {
                ResourceNumber = "RES-01",
                ProcedureCode = "模切分条"
            }
        };
        var viewModel = CreateViewModel(accessor, new StubLoginService(), appSettingsService, new UiBusyService(), new StubPlcStartupConnectionService(), serviceProvider: serviceProvider, clientAppInfoService: clientAppInfoService);

        await viewModel.InitializeAsync();
        accessor.SetCurrentUser(new MhrUser
        {
            CardId = "CARD-01",
            WorkId = "WORK-02",
            AccessLevel = 3
        });

        Assert.Equal(6, viewModel.Tabs.Count());

        viewModel.TabChangedCommand.Execute(3);

        Assert.Equal(1, serviceProvider.GetResolveCount<ToolChangeManagementUserControl>());
        Assert.IsType<ToolChangeManagementUserControl>(viewModel.SelectedContent);
    }

    [Fact]
    public async Task OnTabChanged_WhenNotLoggedInAndSwitchToNonBasicTab_ShouldShowNeedLoginControl()
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
        viewModel.TabChangedCommand.Execute(2);

        Assert.IsType<NeedLoginUserControl>(viewModel.SelectedContent);
    }

    [Fact]
    public async Task OnTabChanged_WhenNotLoggedInAndSwitchToBasicInfoTab_ShouldShowBasicInfoControl()
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
        viewModel.TabChangedCommand.Execute(1);

        Assert.IsType<ClientAppInfoUserControl>(viewModel.SelectedContent);
    }

    [Fact]
    public async Task AppSettingsSaved_WhenOnlyMonitoringFlagChanges_ShouldNotRecreateBasicInfoContent()
    {
        var appSettingsService = new StubAppSettingsService
        {
            Current = new AppSettings
            {
                IsSetClientAppInfo = true,
                IsWearPartMonitoringEnabled = true,
                AutoLogoutCountdownSeconds = 360
            }
        };
        var serviceProvider = new StubServiceProvider();
        var viewModel = CreateViewModel(new CurrentUserAccessor(), new StubLoginService(), appSettingsService, new UiBusyService(), new StubPlcStartupConnectionService(), serviceProvider: serviceProvider);

        await viewModel.InitializeAsync();
        viewModel.TabChangedCommand.Execute(1);
        var selectedContent = viewModel.SelectedContent;
        var resolveCount = serviceProvider.GetResolveCount<ClientAppInfoUserControl>();

        await appSettingsService.SaveAsync(new AppSettings
        {
            IsSetClientAppInfo = true,
            IsWearPartMonitoringEnabled = false,
            AutoLogoutCountdownSeconds = 360,
            ResourceNumber = appSettingsService.Current.ResourceNumber
        });

        Assert.Same(selectedContent, viewModel.SelectedContent);
        Assert.Equal(resolveCount, serviceProvider.GetResolveCount<ClientAppInfoUserControl>());
    }

    [Fact]
    public async Task CurrentUserChanged_OnBasicInfoTab_ShouldNotRecreateBasicInfoContent()
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
        var serviceProvider = new StubServiceProvider();
        var viewModel = CreateViewModel(accessor, new StubLoginService(), appSettingsService, new UiBusyService(), new StubPlcStartupConnectionService(), serviceProvider: serviceProvider);

        await viewModel.InitializeAsync();
        viewModel.TabChangedCommand.Execute(1);
        var selectedContent = viewModel.SelectedContent;
        var resolveCount = serviceProvider.GetResolveCount<ClientAppInfoUserControl>();

        accessor.SetCurrentUser(new MhrUser
        {
            CardId = "CARD-01",
            WorkId = "WORK-02",
            AccessLevel = 3
        });

        Assert.Same(selectedContent, viewModel.SelectedContent);
        Assert.Equal(resolveCount, serviceProvider.GetResolveCount<ClientAppInfoUserControl>());
    }

    [Fact]
    public async Task CurrentUserChanged_WhenLoggedInAfterNeedLoginShown_ShouldRestoreRequestedTabContent()
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
        viewModel.TabChangedCommand.Execute(4);
        Assert.IsType<NeedLoginUserControl>(viewModel.SelectedContent);

        accessor.SetCurrentUser(new MhrUser
        {
            CardId = "CARD-01",
            WorkId = "WORK-02",
            AccessLevel = 3
        });

        Assert.IsType<PartUpdateRecordUserControl>(viewModel.SelectedContent);
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
    public async Task AutoLogoutCountdownTick_WhenLoggedInOnRestrictedTab_ShouldNotRecreateSelectedContent()
    {
        var appSettingsService = new StubAppSettingsService
        {
            Current = new AppSettings
            {
                IsSetClientAppInfo = true,
                AutoLogoutCountdownSeconds = 3
            }
        };
        var accessor = new CurrentUserAccessor();
        var delaySignals = new Queue<TaskCompletionSource<bool>>();
        var loginService = new StubLoginService(accessor);
        var serviceProvider = new StubServiceProvider();
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
            },
            serviceProvider);

        await viewModel.InitializeAsync();

        accessor.SetCurrentUser(new MhrUser
        {
            CardId = "CARD-01",
            WorkId = "WORK-02",
            AccessLevel = 3
        });

        viewModel.TabChangedCommand.Execute(4);
        var selectedContent = viewModel.SelectedContent;
        var partUpdateRecordResolveCountBeforeTick = serviceProvider.GetResolveCount<PartUpdateRecordUserControl>();

        delaySignals.Dequeue().SetResult(true);
        await WaitUntilAsync(() => viewModel.CurrentUserAccessLevelText == LocalizedText.Format("ViewModels.MainWindowVm.CurrentUserAccessLevelCountdown", 3, "00:02"));

        Assert.Same(selectedContent, viewModel.SelectedContent);
        Assert.Equal(partUpdateRecordResolveCountBeforeTick, serviceProvider.GetResolveCount<PartUpdateRecordUserControl>());
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
        var startupCoordinator = new StubAppStartupCoordinator
        {
            PendingTaskSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously),
            LoadingMessages =
            [
                LocalizedText.Get("ViewModels.MainWindowVm.InitializingDatabase"),
                LocalizedText.Get("Services.PlcStartupConnection.Connecting")
            ]
        };
        var viewModel = CreateViewModel(new CurrentUserAccessor(), new StubLoginService(), appSettingsService, new UiBusyService(TimeSpan.Zero), startupConnectionService, appStartupCoordinator: startupCoordinator);

        var initializeTask = viewModel.InitializeAsync();

        await WaitUntilAsync(() => startupCoordinator.CallCount == 1);
        await WaitUntilAsync(() => viewModel.IsBusy);
        await WaitUntilAsync(() => viewModel.LoadingText == LocalizedText.Get("Services.PlcStartupConnection.Connecting"));

        Assert.True(viewModel.IsBusy);
        Assert.False(viewModel.IsNotBusy);
        Assert.Equal(LocalizedText.Get("Services.PlcStartupConnection.Connecting"), viewModel.LoadingText);
        Assert.True(viewModel.HasLoadingText);
        Assert.Equal(0, startupConnectionService.CallCount);

        startupCoordinator.PendingTaskSource.SetResult();
        await initializeTask;
        await WaitUntilAsync(() => !viewModel.IsBusy && string.IsNullOrEmpty(viewModel.LoadingText));

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
        var startupCoordinator = new StubAppStartupCoordinator();
        var viewModel = CreateViewModel(new CurrentUserAccessor(), new StubLoginService(), appSettingsService, new UiBusyService(TimeSpan.Zero), startupConnectionService, appStartupCoordinator: startupCoordinator);

        await viewModel.InitializeAsync();
        await viewModel.InitializeAsync();

        Assert.Equal(1, startupCoordinator.CallCount);
        Assert.Equal(0, startupConnectionService.CallCount);
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

    [Fact]
    public async Task LocalizationRefresh_ShouldUpdateShellTextsAndTabs()
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
        var chineseTitle = viewModel.Title;
        var chineseBrandTitle = viewModel.BrandTitle;
        var chineseFirstTab = viewModel.Tabs.First();

        using var _ = new TestCultureScope("en-US");
        LocalizationBindingSource.Instance.Refresh();

        Assert.NotEqual(chineseTitle, viewModel.Title);
        Assert.NotEqual(chineseBrandTitle, viewModel.BrandTitle);
        Assert.NotEqual(chineseFirstTab, viewModel.Tabs.First());
        Assert.Equal(LocalizedText.Get("MainWindow.Title"), viewModel.Title);
        Assert.Equal(LocalizedText.Get("MainWindowView.BrandTitle"), viewModel.BrandTitle);
        Assert.Equal(LocalizedText.Format("ViewModels.MainWindowVm.SoftwareVersion", ResolveVersionText()), viewModel.SoftwareVersionText);
    }

    [Fact]
    public void Constructor_ShouldSynchronizeLocalizationCultureFromUserConfigBeforeRefreshingShellState()
    {
        var appSettingsService = new StubAppSettingsService
        {
            Current = new AppSettings
            {
                IsSetClientAppInfo = true,
                AutoLogoutCountdownSeconds = 360
            }
        };
        var localizationService = new MutableLocalizationService("zh-CN");
        var userConfigService = new StubUserConfigService
        {
            Current = new UserConfig
            {
                Language = "en-US"
            }
        };

        var viewModel = CreateViewModel(
            new CurrentUserAccessor(),
            new StubLoginService(),
            appSettingsService,
            new UiBusyService(),
            new StubPlcStartupConnectionService(),
            localizationService: localizationService,
            userConfigService: userConfigService);

        Assert.Equal("en-US", localizationService.CurrentCulture.Name);
        Assert.Equal("en-US", localizationService.LastSetCultureName);
        Assert.Equal(LocalizedText.Get("MainWindow.Title"), viewModel.Title);
    }

    [Fact]
    public async Task LocalizationRefresh_WhenUserConfigTabSelected_ShouldNotRecreateSelectedContent()
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
        var serviceProvider = new StubServiceProvider();
        var viewModel = CreateViewModel(accessor, new StubLoginService(), appSettingsService, new UiBusyService(), new StubPlcStartupConnectionService(), serviceProvider: serviceProvider);

        await viewModel.InitializeAsync();
        accessor.SetCurrentUser(new MhrUser
        {
            CardId = "CARD-01",
            WorkId = "WORK-02",
            AccessLevel = 3
        });

        viewModel.TabChangedCommand.Execute(5);
        var selectedContent = viewModel.SelectedContent;
        var resolveCount = serviceProvider.GetResolveCount<UserConfigUserControl>();

        using var _ = new TestCultureScope("en-US");
        LocalizationBindingSource.Instance.Refresh();

        Assert.Same(selectedContent, viewModel.SelectedContent);
        Assert.Equal(resolveCount, serviceProvider.GetResolveCount<UserConfigUserControl>());
        Assert.Equal(LocalizedText.Get("MainWindow.Title"), viewModel.Title);
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
                IsSetClientAppInfo = Current.IsSetClientAppInfo,
                IsWearPartMonitoringEnabled = Current.IsWearPartMonitoringEnabled
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

    private static string ResolveVersionText()
    {
        var version = typeof(MainWindowViewModel).Assembly.GetName().Version;
        if (version is null)
        {
            return "--";
        }

        return version.Build >= 0
            ? version.ToString(3)
            : version.ToString();
    }

    private static MainWindowViewModel CreateViewModel(
        CurrentUserAccessor accessor,
        StubLoginService loginService,
        StubAppSettingsService appSettingsService,
        IUiBusyService uiBusyService,
        IPlcStartupConnectionService startupConnectionService,
        Func<TimeSpan, CancellationToken, Task>? delayAsync = null,
        StubServiceProvider? serviceProvider = null,
        StubAppStartupCoordinator? appStartupCoordinator = null,
        StubClientAppInfoService? clientAppInfoService = null,
        ILocalizationService? localizationService = null,
        StubUserConfigService? userConfigService = null)
    {
        var stateMachine = new LoginSessionStateMachine(accessor, loginService, delayAsync);
        return new MainWindowViewModel(
            localizationService ?? new StubLocalizationService(),
            serviceProvider ?? new StubServiceProvider(),
            loginService,
            appSettingsService,
            userConfigService ?? new StubUserConfigService(),
            clientAppInfoService ?? new StubClientAppInfoService(),
            uiBusyService,
            startupConnectionService,
            stateMachine,
                new StubUiDispatcher(),
                appStartupCoordinator ?? new StubAppStartupCoordinator());
    }

    private sealed class StubAppStartupCoordinator : IAppStartupCoordinator
    {
        public int CallCount { get; private set; }

        public TaskCompletionSource? PendingTaskSource { get; set; }

        public IReadOnlyList<string> LoadingMessages { get; set; } = [];

        public async Task EnsureInitializedAsync(Func<string, Task>? reportLoadingAsync = null, CancellationToken cancellationToken = default)
        {
            CallCount++;

            foreach (var message in LoadingMessages)
            {
                if (reportLoadingAsync is not null)
                {
                    await reportLoadingAsync(message);
                }
            }

            if (PendingTaskSource is not null)
            {
                cancellationToken.Register(() => PendingTaskSource.TrySetCanceled(cancellationToken));
                await PendingTaskSource.Task;
            }
        }
    }

    private sealed class StubClientAppInfoService : IClientAppInfoService
    {
        public ClientAppInfoModel Model { get; set; } = new()
        {
            ProcedureCode = "模切分条"
        };

        public Task<ClientAppInfoModel> GetAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Model);
        }

        public Task<ClientAppInfoModel> SaveAsync(ClientAppInfoSaveRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class StubLocalizationService : ILocalizationService
    {
        public string this[string name] => LocalizedText.Get(name);

        public LocalizationCatalog Catalog { get; } = new(LocalizedText.Get);

        public ValueTask InitializeAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public ValueTask SetCultureAsync(string cultureName, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public CultureInfo CurrentCulture { get; } = CultureInfo.InvariantCulture;
    }

    private sealed class MutableLocalizationService : ILocalizationService
    {
        public MutableLocalizationService(string cultureName)
        {
            CurrentCulture = CultureInfo.GetCultureInfo(cultureName);
            ApplyCulture(CurrentCulture);
        }

        public string this[string name] => LocalizedText.Get(name);

        public LocalizationCatalog Catalog => new(LocalizedText.Get);

        public CultureInfo CurrentCulture { get; private set; }

        public string? LastSetCultureName { get; private set; }

        public ValueTask InitializeAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public ValueTask SetCultureAsync(string cultureName, CancellationToken cancellationToken = default)
        {
            LastSetCultureName = cultureName;
            CurrentCulture = CultureInfo.GetCultureInfo(cultureName);
            ApplyCulture(CurrentCulture);
            LocalizationBindingSource.Instance.Refresh();
            return ValueTask.CompletedTask;
        }

        private static void ApplyCulture(CultureInfo culture)
        {
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;
        }
    }

    private sealed class StubUserConfigService : IUserConfigService
    {
        public UserConfig Current { get; set; } = new();

        public ValueTask<UserConfig> GetAsync(CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(new UserConfig
            {
                Language = Current.Language
            });
        }

        public ValueTask SaveAsync(UserConfig config, CancellationToken cancellationToken = default)
        {
            Current = config;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class StubServiceProvider : IServiceProvider
    {
        private readonly Dictionary<Type, int> _resolveCounts = new();
        private readonly HashSet<Type> _supportedTypes =
        [
            typeof(ReplacePartUserControl),
            typeof(ClientAppInfoUserControl),
            typeof(NeedLoginUserControl),
            typeof(PartManagementUserControl),
            typeof(ToolChangeManagementUserControl),
            typeof(PartUpdateRecordUserControl),
            typeof(UserConfigUserControl)
        ];

        public object? GetService(Type serviceType)
        {
            if (!_supportedTypes.Contains(serviceType))
            {
                return null;
            }

            _resolveCounts[serviceType] = GetResolveCount(serviceType) + 1;
            return RuntimeHelpers.GetUninitializedObject(serviceType);
        }

        public int GetResolveCount<T>() where T : class
        {
            return GetResolveCount(typeof(T));
        }

        private int GetResolveCount(Type serviceType)
        {
            return _resolveCounts.TryGetValue(serviceType, out var count)
                ? count
                : 0;
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