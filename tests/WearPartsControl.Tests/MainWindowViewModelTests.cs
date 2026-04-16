using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Runtime.CompilerServices;
using WearPartsControl.ApplicationServices;
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
        var accessor = new CurrentUserAccessor();
        var viewModel = new MainWindowViewModel(
            new StubLocalizationService(),
            new StubServiceProvider(),
            accessor,
            new StubLoginService());

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
        Assert.Equal("切换登录", viewModel.LoginButtonText);
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
            [typeof(ReplacePartUserControl)] = RuntimeHelpers.GetUninitializedObject(typeof(ReplacePartUserControl))
        };

        public object? GetService(Type serviceType)
        {
            return _services.TryGetValue(serviceType, out var service)
                ? service
                : null;
        }
    }
}