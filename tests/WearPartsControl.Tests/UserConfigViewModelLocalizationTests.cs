using WearPartsControl.ApplicationServices;
using WearPartsControl.ApplicationServices.ClientAppInfo;
using WearPartsControl.ApplicationServices.ComNotification;
using WearPartsControl.ApplicationServices.Localization;
using WearPartsControl.ApplicationServices.Localization.Generated;
using WearPartsControl.ApplicationServices.UserConfig;
using WearPartsControl.ViewModels;
using Xunit;

namespace WearPartsControl.Tests;

[Collection(LocalizationSensitiveTestCollection.Name)]
public sealed class UserConfigViewModelLocalizationTests
{
    [Fact]
    public void Constructor_ShouldLocalizeLanguageOptions_ForChineseCulture()
    {
        using var cultureScope = new TestCultureScope("zh-CN");

        var viewModel = CreateViewModel("zh-CN");

        Assert.Collection(
            viewModel.LanguageOptions,
            option =>
            {
                Assert.Equal("zh-CN", option.Code);
                Assert.Equal("简体中文", option.DisplayName);
            },
            option =>
            {
                Assert.Equal("en-US", option.Code);
                Assert.Equal("英文", option.DisplayName);
            });
    }

    [Fact]
    public async Task SaveCommand_ShouldRefreshLanguageOptions_AfterCultureSwitch()
    {
        using var cultureScope = new TestCultureScope("zh-CN");

        var localizationService = new MutableLocalizationService("zh-CN");
        var viewModel = new UserConfigViewModel(new StubClientAppInfoService(), new StubUserConfigService(), new StubComNotificationService(), localizationService, new StubUiDispatcher(), new UiBusyService(TimeSpan.Zero));

        viewModel.SelectedLanguage = "en-US";
        var selectedOption = viewModel.SelectedLanguageOption;

        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.Collection(
            viewModel.LanguageOptions,
            option =>
            {
                Assert.Equal("zh-CN", option.Code);
                Assert.Equal("Simplified Chinese", option.DisplayName);
            },
            option =>
            {
                Assert.Equal("en-US", option.Code);
                Assert.Equal("English", option.DisplayName);
            });

        Assert.Equal("en-US", viewModel.SelectedLanguage);
        Assert.NotNull(viewModel.SelectedLanguageOption);
        Assert.Equal("en-US", viewModel.SelectedLanguageOption!.Code);
        Assert.Same(selectedOption, viewModel.SelectedLanguageOption);
    }

    [Fact]
    public void LocalizationRefresh_ShouldUpdateLanguageOptionsWithoutSaving()
    {
        using var cultureScope = new TestCultureScope("zh-CN");

        var viewModel = CreateViewModel("zh-CN");

        using var _ = new TestCultureScope("en-US");
        LocalizationBindingSource.Instance.Refresh();

        Assert.Collection(
            viewModel.LanguageOptions,
            option => Assert.Equal("Simplified Chinese", option.DisplayName),
            option => Assert.Equal("English", option.DisplayName));
    }

    private static UserConfigViewModel CreateViewModel(string cultureName)
    {
        return new UserConfigViewModel(new StubClientAppInfoService(), new StubUserConfigService(), new StubComNotificationService(), new MutableLocalizationService(cultureName), new StubUiDispatcher(), new UiBusyService(TimeSpan.Zero));
    }

    private sealed class MutableLocalizationService : ILocalizationService
    {
        public MutableLocalizationService(string cultureName)
        {
            CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo(cultureName);
        }

        public event EventHandler? CultureChanged;

        public LocalizationCatalog Catalog => new(LocalizedText.Get);

        public System.Globalization.CultureInfo CurrentCulture { get; private set; }

        public string this[string key] => LocalizedText.Get(key);

        public ValueTask InitializeAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public ValueTask SetCultureAsync(string cultureName, CancellationToken cancellationToken = default)
        {
            CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo(cultureName);
            var culture = CurrentCulture;
            System.Globalization.CultureInfo.CurrentCulture = culture;
            System.Globalization.CultureInfo.CurrentUICulture = culture;
            System.Globalization.CultureInfo.DefaultThreadCurrentCulture = culture;
            System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = culture;
            CultureChanged?.Invoke(this, EventArgs.Empty);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class StubClientAppInfoService : IClientAppInfoService
    {
        public Task<ClientAppInfoModel> GetAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ClientAppInfoModel());
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
            return ValueTask.FromResult(new UserConfig());
        }

        public ValueTask SaveAsync(UserConfig config, CancellationToken cancellationToken = default)
        {
            return ValueTask.CompletedTask;
        }
    }

    private sealed class StubComNotificationService : IComNotificationService
    {
        public ValueTask NotifyGroupAsync(string title, string text, IReadOnlyCollection<string>? toUsers = null, CancellationToken cancellationToken = default)
        {
            return ValueTask.CompletedTask;
        }

        public ValueTask NotifyWorkAsync(string title, string text, IReadOnlyCollection<string>? toUsers = null, CancellationToken cancellationToken = default)
        {
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