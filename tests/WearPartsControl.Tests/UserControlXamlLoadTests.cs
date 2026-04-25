using System;
using System.Runtime.CompilerServices;
using WearPartsControl.ApplicationServices;
using WearPartsControl.ApplicationServices.AppSettings;
using WearPartsControl.ApplicationServices.Dialogs;
using WearPartsControl.ApplicationServices.LoginService;
using WearPartsControl.ApplicationServices.PartServices;
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

    [Fact]
    public void ToolChangeManagementUserControl_ShouldLoadWithoutXamlParseException()
    {
        WpfTestHost.Run(() =>
        {
            var control = new ToolChangeManagementUserControl(
                new ToolChangeManagementViewModel(
                    new StubToolChangeManagementService(),
                    new StubUiDispatcher(),
                    new UiBusyService(TimeSpan.Zero),
                    new StubAppDialogService()));

            Assert.NotNull(control);
        }, ensureApplicationResources: true);
    }

    [Fact]
    public void PartInfoUserControl_ShouldLoadWithoutXamlParseException()
    {
        WpfTestHost.Run(() =>
        {
            var control = new PartInfoUserControl();

            Assert.NotNull(control);
        }, ensureApplicationResources: true);
    }

    [Fact]
    public void MainWindowTrayContentControl_ShouldLoadWithoutXamlParseException()
    {
        WpfTestHost.Run(() =>
        {
            var control = new MainWindowTrayContentControl();

            Assert.NotNull(control);
        }, ensureApplicationResources: true);
    }

    [Fact]
    public void LoginBox_ShouldLoadWithoutXamlParseException()
    {
        WpfTestHost.Run(() =>
        {
            var control = new LoginBox();

            Assert.NotNull(control);
        }, ensureApplicationResources: true);
    }

    [Fact]
    public void NeedLoginUserControl_ShouldLoadWithoutXamlParseException()
    {
        WpfTestHost.Run(() =>
        {
            var control = new NeedLoginUserControl(new NeedLoginViewModel());

            Assert.NotNull(control);
        }, ensureApplicationResources: true);
    }

    [Fact]
    public void UserTabControl_ShouldLoadWithoutXamlParseException()
    {
        WpfTestHost.Run(() =>
        {
            var control = new UserTabControl();

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

    private sealed class StubAppDialogService : IAppDialogService
    {
        public bool ShowDialog(System.Windows.Window dialog, System.Windows.Window? owner = null)
        {
            return false;
        }

        public System.Windows.MessageBoxResult ShowMessage(string message, string title, System.Windows.MessageBoxButton buttons = System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage image = System.Windows.MessageBoxImage.None, System.Windows.Window? owner = null, System.Windows.MessageBoxResult defaultResult = System.Windows.MessageBoxResult.None)
        {
            return System.Windows.MessageBoxResult.OK;
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

    private sealed class StubUiDispatcher : IUiDispatcher
    {
        public void Run(Action action) => action();

        public Task RunAsync(Action action, System.Windows.Threading.DispatcherPriority priority = System.Windows.Threading.DispatcherPriority.Normal)
        {
            action();
            return Task.CompletedTask;
        }

        public Task RenderAsync() => Task.CompletedTask;
    }

    private sealed class StubToolChangeManagementService : IToolChangeManagementService
    {
        public Task<IReadOnlyList<ToolChangeDefinition>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<ToolChangeDefinition>>(Array.Empty<ToolChangeDefinition>());
        }

        public Task<ToolChangeDefinition> CreateAsync(ToolChangeDefinition definition, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<ToolChangeDefinition> UpdateAsync(ToolChangeDefinition definition, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}