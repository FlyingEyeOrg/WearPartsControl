using System.Windows;
using WearPartsControl.ApplicationServices;
using WearPartsControl.ApplicationServices.AppSettings;
using WearPartsControl.ApplicationServices.ClientAppInfo;
using WearPartsControl.ApplicationServices.Dialogs;
using WearPartsControl.ApplicationServices.LoginService;
using WearPartsControl.ApplicationServices.PartServices;
using WearPartsControl.ViewModels;
using Xunit;

namespace WearPartsControl.Tests;

[Collection(LocalizationSensitiveTestCollection.Name)]
public sealed class WearPartValuePreviewViewModelTests
{
    [Fact]
    public async Task InitializeAsync_WhenThresholdSyncFlagDisabled_ShouldKeepSyncCommandDisabled()
    {
        var viewModel = CreateViewModel(isThresholdSyncToDeviceEnabled: false);

        await viewModel.InitializeAsync();

        Assert.False(viewModel.SyncThresholdsCommand.CanExecute(null));
    }

    [Fact]
    public async Task InitializeAsync_WhenThresholdSyncFlagEnabled_ShouldAllowSyncCommand()
    {
        var viewModel = CreateViewModel(isThresholdSyncToDeviceEnabled: true);

        await viewModel.InitializeAsync();

        Assert.True(viewModel.SyncThresholdsCommand.CanExecute(null));
    }

    private static WearPartValuePreviewViewModel CreateViewModel(bool isThresholdSyncToDeviceEnabled)
    {
        var currentUserAccessor = new CurrentUserAccessor();
        currentUserAccessor.SetCurrentUser(new MhrUser
        {
            CardId = "CARD-01",
            WorkId = "WORK-01",
            AccessLevel = 4
        });

        return new WearPartValuePreviewViewModel(
            new StubAppSettingsService
            {
                Current = new AppSettings
                {
                    ResourceNumber = "RES-01",
                    IsThresholdSyncToDeviceEnabled = isThresholdSyncToDeviceEnabled
                }
            },
            new StubClientAppInfoService
            {
                Model = new ClientAppInfoModel
                {
                    Id = Guid.NewGuid(),
                    ResourceNumber = "RES-01"
                }
            },
            new StubDialogService(),
            currentUserAccessor,
            new StubWearPartValuePreviewService(),
            new ImmediateUiDispatcher(),
            new UiBusyService(TimeSpan.Zero));
    }

    private sealed class StubAppSettingsService : IAppSettingsService
    {
        public event EventHandler<AppSettings>? SettingsSaved;

        public AppSettings Current { get; set; } = new();

        public ValueTask<AppSettings> GetAsync(CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(Current);
        }

        public ValueTask SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
        {
            Current = settings;
            SettingsSaved?.Invoke(this, settings);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class StubClientAppInfoService : IClientAppInfoService
    {
        public ClientAppInfoModel Model { get; set; } = new();

        public Task<ClientAppInfoModel> GetAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Model);
        }

        public Task<ClientAppInfoModel> SaveAsync(ClientAppInfoSaveRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class StubDialogService : IAppDialogService
    {
        public bool ShowDialog(Window dialog, Window? owner = null)
        {
            throw new NotSupportedException();
        }

        public MessageBoxResult ShowMessage(
            string message,
            string title,
            MessageBoxButton buttons = MessageBoxButton.OK,
            MessageBoxImage image = MessageBoxImage.None,
            Window? owner = null,
            MessageBoxResult defaultResult = MessageBoxResult.None)
        {
            return MessageBoxResult.No;
        }
    }

    private sealed class StubWearPartValuePreviewService : IWearPartValuePreviewService
    {
        private static readonly IReadOnlyList<WearPartValuePreviewItem> PreviewItems =
        [
            new WearPartValuePreviewItem
            {
                WearPartDefinitionId = Guid.NewGuid(),
                ClientAppConfigurationId = Guid.NewGuid(),
                ResourceNumber = "RES-01",
                PartName = "刀具A",
                WearPartTypeName = "刀具",
                LifetimeType = "Count",
                CurrentValue = 10,
                WarningValue = 20,
                ShutdownValue = 30,
                ConfiguredWarningLifetimeThreshold = 18,
                ConfiguredShutdownLifetimeThreshold = 28,
                Status = WearPartMonitorStatus.Normal
            }
        ];

        public Task<IReadOnlyList<WearPartValuePreviewItem>> GetByResourceNumberAsync(string resourceNumber, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(PreviewItems);
        }

        public Task<IReadOnlyList<WearPartValuePreviewItem>> SyncConfiguredThresholdsToDeviceAsync(string resourceNumber, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(PreviewItems);
        }
    }

    private sealed class ImmediateUiDispatcher : IUiDispatcher
    {
        public void Run(Action action)
        {
            action();
        }

        public Task RunAsync(Action action, System.Windows.Threading.DispatcherPriority priority = System.Windows.Threading.DispatcherPriority.Normal)
        {
            action();
            return Task.CompletedTask;
        }

        public Task RenderAsync()
        {
            return Task.CompletedTask;
        }
    }
}