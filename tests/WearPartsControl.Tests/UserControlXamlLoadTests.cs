using System;
using System.Runtime.CompilerServices;
using WearPartsControl.ApplicationServices;
using WearPartsControl.ApplicationServices.AppSettings;
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
        WpfTestHost.Run(() =>
        {
            var control = new ReplacePartUserControl(
                CreateUninitialized<ReplacePartViewModel>(),
                new StubAutoLogoutInteractionService());

            Assert.NotNull(control);
        }, ensureApplicationResources: true);
    }

    [Fact]
    public void PartManagementUserControl_ShouldLoadWithoutXamlParseException()
    {
        WpfTestHost.Run(() =>
        {
            var control = new PartManagementUserControl(
                CreateUninitialized<PartManagementViewModel>(),
                new StubServiceProvider(),
                new StubAutoLogoutInteractionService());

            Assert.NotNull(control);
        }, ensureApplicationResources: true);
    }

    [Fact]
    public void PartUpdateRecordUserControl_ShouldLoadWithoutXamlParseException()
    {
        WpfTestHost.Run(() =>
        {
            var control = new PartUpdateRecordUserControl(
                CreateUninitialized<PartUpdateRecordViewModel>(),
                new StubAutoLogoutInteractionService());

            Assert.NotNull(control);
        }, ensureApplicationResources: true);
    }

    [Fact]
    public void ClientAppInfoUserControl_ShouldLoadWithoutXamlParseException()
    {
        WpfTestHost.Run(() =>
        {
            var control = new ClientAppInfoUserControl(
                CreateUninitialized<ClientAppInfoViewModel>(),
                new StubServiceProvider(),
                new StubAutoLogoutInteractionService(),
                new StubCurrentUserAccessor(),
                new StubAppSettingsService());

            Assert.NotNull(control);
        }, ensureApplicationResources: true);
    }

    private static T CreateUninitialized<T>() where T : class
    {
        return (T)RuntimeHelpers.GetUninitializedObject(typeof(T));
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

    private sealed class StubAppSettingsService : IAppSettingsService
    {
        public event EventHandler<AppSettings>? SettingsSaved;

        public ValueTask<AppSettings> GetAsync(CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(new AppSettings());
        }

        public ValueTask SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
        {
            SettingsSaved?.Invoke(this, settings);
            return ValueTask.CompletedTask;
        }
    }
}