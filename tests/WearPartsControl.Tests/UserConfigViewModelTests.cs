using WearPartsControl.ApplicationServices.ComNotification;
using WearPartsControl.ApplicationServices.Localization;
using WearPartsControl.ApplicationServices.UserConfig;
using WearPartsControl.ApplicationServices;
using WearPartsControl.ViewModels;
using Xunit;

namespace WearPartsControl.Tests;

public sealed class UserConfigViewModelTests
{
    [Fact]
    public async Task InitializeAsync_ShouldLoadSavedUserConfig()
    {
        var service = new StubUserConfigService
        {
            Current = new UserConfig
            {
                MeResponsibleWorkId = "ME001",
                PrdResponsibleWorkId = "PRD001",
                ComAccessToken = "token",
                ComSecret = "secret"
            }
        };
        var viewModel = new UserConfigViewModel(service, new StubComNotificationService(), new StubUiDispatcher());

        await viewModel.InitializeAsync();

        Assert.Equal("ME001", viewModel.MeResponsibleWorkId);
        Assert.Equal("PRD001", viewModel.PrdResponsibleWorkId);
        Assert.Equal("token", viewModel.ComAccessToken);
        Assert.Equal("secret", viewModel.ComSecret);
        Assert.False(viewModel.IsDirty);
        Assert.Equal(LocalizedText.Get("ViewModels.UserConfigVm.Loaded"), viewModel.StatusMessage);
        Assert.False(viewModel.IsBusy);
    }

    [Fact]
    public async Task SaveCommand_ShouldPersistCurrentValuesAndClearDirtyFlag()
    {
        var service = new StubUserConfigService();
        var viewModel = new UserConfigViewModel(service, new StubComNotificationService(), new StubUiDispatcher());
        await viewModel.InitializeAsync();

        viewModel.MeResponsibleWorkId = "ME002";
        viewModel.PrdResponsibleWorkId = "PRD002";
        viewModel.ComAccessToken = "token-2";
        viewModel.ComSecret = "secret-2";

        Assert.True(viewModel.IsDirty);
        Assert.True(viewModel.SaveCommand.CanExecute(null));

        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.False(viewModel.IsDirty);
        Assert.Equal(LocalizedText.Get("ViewModels.UserConfigVm.Saved"), viewModel.StatusMessage);
        Assert.NotNull(service.LastSaved);
        Assert.Equal("ME002", service.LastSaved!.MeResponsibleWorkId);
        Assert.Equal("PRD002", service.LastSaved.PrdResponsibleWorkId);
    }

    [Fact]
    public async Task TestComNotificationCommand_ShouldSaveDirtyValuesAndSendToDistinctRecipients()
    {
        var service = new StubUserConfigService();
        var notificationService = new StubComNotificationService();
        var viewModel = new UserConfigViewModel(service, notificationService, new StubUiDispatcher());
        await viewModel.InitializeAsync();

        viewModel.MeResponsibleWorkId = "ME003";
        viewModel.PrdResponsibleWorkId = "ME003";
        viewModel.ComAccessToken = "token-3";
        viewModel.ComSecret = "secret-3";

        await viewModel.TestComNotificationCommand.ExecuteAsync(null);

        Assert.NotNull(service.LastSaved);
        Assert.NotNull(notificationService.LastUsers);
        Assert.Single(notificationService.LastUsers!);
        Assert.Contains("ME003", notificationService.LastUsers!);
        Assert.Equal(LocalizedText.Get("ViewModels.UserConfigVm.TestSucceeded"), viewModel.StatusMessage);
        Assert.False(viewModel.IsDirty);
    }

    private sealed class StubUserConfigService : IUserConfigService
    {
        public UserConfig Current { get; set; } = new();

        public UserConfig? LastSaved { get; private set; }

        public ValueTask<UserConfig> GetAsync(CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(new UserConfig
            {
                MeResponsibleWorkId = Current.MeResponsibleWorkId,
                PrdResponsibleWorkId = Current.PrdResponsibleWorkId,
                ComAccessToken = Current.ComAccessToken,
                ComSecret = Current.ComSecret
            });
        }

        public ValueTask SaveAsync(UserConfig config, CancellationToken cancellationToken = default)
        {
            LastSaved = new UserConfig
            {
                MeResponsibleWorkId = config.MeResponsibleWorkId,
                PrdResponsibleWorkId = config.PrdResponsibleWorkId,
                ComAccessToken = config.ComAccessToken,
                ComSecret = config.ComSecret
            };
            Current = LastSaved;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class StubComNotificationService : IComNotificationService
    {
        public IReadOnlyCollection<string>? LastUsers { get; private set; }

        public ValueTask NotifyGroupAsync(string title, string text, IReadOnlyCollection<string>? toUsers = null, CancellationToken cancellationToken = default)
        {
            LastUsers = toUsers;
            return ValueTask.CompletedTask;
        }

        public ValueTask NotifyWorkAsync(string title, string text, IReadOnlyCollection<string>? toUsers = null, CancellationToken cancellationToken = default)
        {
            LastUsers = toUsers;
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
}