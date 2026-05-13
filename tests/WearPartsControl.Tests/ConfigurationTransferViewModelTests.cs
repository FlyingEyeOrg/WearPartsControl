using WearPartsControl.ApplicationServices;
using WearPartsControl.ApplicationServices.AppSettings;
using WearPartsControl.ApplicationServices.ConfigurationTransfer;
using WearPartsControl.ApplicationServices.Localization;
using WearPartsControl.ViewModels;
using Xunit;

namespace WearPartsControl.Tests;

public sealed class ConfigurationTransferViewModelTests
{
    [Fact]
    public async Task ExportAsync_ShouldRenderBeforeStartingTransfer()
    {
        var uiDispatcher = new TrackingUiDispatcher();
        var configurationTransferService = new TrackingConfigurationTransferService(() => uiDispatcher.RenderCallCount > 0);
        var viewModel = new ConfigurationTransferViewModel(
            configurationTransferService,
            new StubAppSettingsService(),
            uiDispatcher,
            new UiBusyService(TimeSpan.Zero));

        await viewModel.ExportAsync("config.cfg");

        Assert.True(configurationTransferService.RenderObservedBeforeExport);
    }

    [Fact]
    public async Task ImportAsync_ShouldRenderBeforeStartingTransfer()
    {
        var uiDispatcher = new TrackingUiDispatcher();
        var configurationTransferService = new TrackingConfigurationTransferService(() => uiDispatcher.RenderCallCount > 0);
        var viewModel = new ConfigurationTransferViewModel(
            configurationTransferService,
            new StubAppSettingsService(),
            uiDispatcher,
            new UiBusyService(TimeSpan.Zero));

        await viewModel.ImportAsync("config.cfg");

        Assert.True(configurationTransferService.RenderObservedBeforeImport);
    }

    [Fact]
    public async Task InitializeAsync_WhenClientInfoConfigured_ShouldKeepImportEnabled()
    {
        var viewModel = new ConfigurationTransferViewModel(
            new TrackingConfigurationTransferService(() => true),
            new StubAppSettingsService(new AppSettings
            {
                ResourceNumber = "TARGET-01",
                IsSetClientAppInfo = true
            }),
            new TrackingUiDispatcher(),
            new UiBusyService(TimeSpan.Zero));

        await viewModel.InitializeAsync();

        Assert.True(viewModel.CanImport);
        Assert.Equal(LocalizedText.Get("ViewModels.ConfigurationTransferVm.ImportUnavailableConfigured"), viewModel.ImportAvailabilityMessage);
    }

    private sealed class TrackingConfigurationTransferService : IConfigurationTransferService
    {
        private readonly Func<bool> _hasRendered;

        public TrackingConfigurationTransferService(Func<bool> hasRendered)
        {
            _hasRendered = hasRendered;
        }

        public bool RenderObservedBeforeExport { get; private set; }

        public bool RenderObservedBeforeImport { get; private set; }

        public Task<ConfigurationTransferSummary> ExportAsync(string packagePath, CancellationToken cancellationToken = default)
        {
            RenderObservedBeforeExport = _hasRendered();
            return Task.FromResult(new ConfigurationTransferSummary(packagePath, 1));
        }

        public Task<ConfigurationTransferSummary> ImportAsync(string packagePath, CancellationToken cancellationToken = default)
        {
            RenderObservedBeforeImport = _hasRendered();
            return Task.FromResult(new ConfigurationTransferSummary(packagePath, 1));
        }
    }

    private sealed class StubAppSettingsService : IAppSettingsService
    {
        private AppSettings _settings;

        public StubAppSettingsService(AppSettings? settings = null)
        {
            _settings = settings ?? new AppSettings();
        }

        public event EventHandler<AppSettings>? SettingsSaved;

        public ValueTask<AppSettings> GetAsync(CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(_settings);
        }

        public ValueTask SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
        {
            _settings = settings;
            SettingsSaved?.Invoke(this, settings);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class TrackingUiDispatcher : IUiDispatcher
    {
        public int RenderCallCount { get; private set; }

        public void Run(Action action)
        {
            ArgumentNullException.ThrowIfNull(action);
            action();
        }

        public Task RunAsync(Action action, System.Windows.Threading.DispatcherPriority priority = System.Windows.Threading.DispatcherPriority.Normal)
        {
            ArgumentNullException.ThrowIfNull(action);
            action();
            return Task.CompletedTask;
        }

        public Task RenderAsync()
        {
            RenderCallCount++;
            return Task.CompletedTask;
        }
    }
}