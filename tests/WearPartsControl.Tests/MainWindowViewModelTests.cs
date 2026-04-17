using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Runtime.CompilerServices;
using WearPartsControl.ApplicationServices;
using WearPartsControl.ApplicationServices.AppSettings;
using WearPartsControl.ApplicationServices.Localization;
using WearPartsControl.ApplicationServices.Localization.Generated;
using WearPartsControl.ApplicationServices.LoginService;
using WearPartsControl.UserControls;
using WearPartsControl.ViewModels;
using Xunit;

namespace WearPartsControl.Tests;

public sealed class MainWindowViewModelTests
{
    [Fact]
    public void CurrentUserChanged_ShouldRefreshLoginStatus()
    {
        var appSettingsService = new StubAppSettingsService
        {
            Current = new AppSettings
            {
                IsSetClientAppInfo = true
            }
        };
        var accessor = new CurrentUserAccessor();
        var viewModel = new MainWindowViewModel(
            new StubLocalizationService(),
            new StubServiceProvider(),
            accessor,
            new StubLoginService(),
            appSettingsService,
            new UiBusyService());

        Assert.True(viewModel.IsClientAppInfoConfigured);
        Assert.Equal("工号：--", viewModel.CurrentUserWorkIdText);
        Assert.Equal("权限：--", viewModel.CurrentUserAccessLevelText);
        Assert.False(viewModel.IsLoggedIn);

        accessor.SetCurrentUser(new MhrUser
        {
            CardId = "CARD-01",
            WorkId = "WORK-02",
            AccessLevel = 3
        });

        Assert.Equal("工号：WORK-02", viewModel.CurrentUserWorkIdText);
        Assert.Equal("权限：3", viewModel.CurrentUserAccessLevelText);
        Assert.True(viewModel.IsLoggedIn);
        Assert.False(viewModel.ShowLoginButton);
        Assert.True(viewModel.ShowLogoutButton);
        Assert.True(viewModel.LogoutCommand.CanExecute(null));
    }

    [Fact]
    public void InitialState_WhenNoUser_ShouldShowLoginAndDisableLogout()
    {
        var appSettingsService = new StubAppSettingsService
        {
            Current = new AppSettings
            {
                IsSetClientAppInfo = true
            }
        };

        var viewModel = new MainWindowViewModel(
            new StubLocalizationService(),
            new StubServiceProvider(),
            new CurrentUserAccessor(),
            new StubLoginService(),
            appSettingsService,
            new UiBusyService());

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
                IsSetClientAppInfo = false
            }
        };

        var viewModel = new MainWindowViewModel(
            new StubLocalizationService(),
            new StubServiceProvider(),
            new CurrentUserAccessor(),
            new StubLoginService(),
            appSettingsService,
            new UiBusyService());

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
        public AppSettings Current { get; set; } = new();

        public event EventHandler<AppSettings>? SettingsSaved;

        public ValueTask<AppSettings> GetAsync(CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(new AppSettings
            {
                ResourceNumber = Current.ResourceNumber,
                LoginInputMaxIntervalMilliseconds = Current.LoginInputMaxIntervalMilliseconds,
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
}