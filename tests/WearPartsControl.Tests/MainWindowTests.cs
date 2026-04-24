using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
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

            var window = new MainWindow(viewModel, new StubServiceProvider(), new StubAutoLogoutInteractionService());

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
        public Task<MhrUser?> LoginAsync(string authId, string factory, string resourceId, bool isIdCard, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public MhrUser? GetCurrentUser() => null;

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
            if (serviceType == typeof(ClientAppInfoUserControl))
            {
                return new ClientAppInfoUserControl(
                    (ClientAppInfoViewModel)RuntimeHelpers.GetUninitializedObject(typeof(ClientAppInfoViewModel)),
                    this,
                    new StubAutoLogoutInteractionService(),
                    new StubCurrentUserAccessor(),
                    new StubAppSettingsService());
            }

            return serviceType == typeof(NeedLoginUserControl)
                ? RuntimeHelpers.GetUninitializedObject(serviceType)
                : null;
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
}