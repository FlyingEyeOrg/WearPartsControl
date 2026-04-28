using System.Globalization;
using WearPartsControl.ApplicationServices.Localization;
using WearPartsControl.ApplicationServices.SaveInfoService;
using WearPartsControl.ApplicationServices.UserConfig;
using Xunit;

namespace WearPartsControl.Tests;

[Collection(LocalizationSensitiveTestCollection.Name)]
public sealed class LocalizationServiceTests : IDisposable
{
    private readonly CultureInfo _originalCurrentCulture = CultureInfo.CurrentCulture;
    private readonly CultureInfo _originalCurrentUiCulture = CultureInfo.CurrentUICulture;
    private readonly CultureInfo? _originalDefaultThreadCurrentCulture = CultureInfo.DefaultThreadCurrentCulture;
    private readonly CultureInfo? _originalDefaultThreadCurrentUiCulture = CultureInfo.DefaultThreadCurrentUICulture;

    [Fact]
    public async Task Catalog_ShouldReturnStructuredTabs_ForEnglishCulture()
    {
        var store = new FakeSaveInfoStore();
        var service = CreateService(store);

        await service.SetCultureAsync("en-US");

        Assert.Equal("WearPartsControl", service.Catalog.MainWindow.Title);
        Assert.Equal(new[]
        {
            "Wear Part Replacement",
            "Equipment Information",
            "Wear Part Management",
            "Tool Change Management",
            "Replacement History",
            "User Settings"
        }, service.Catalog.MainWindow.Tabs);
    }

    [Fact]
    public async Task Catalog_ShouldReturnStructuredTabs_ForChineseCulture()
    {
        var store = new FakeSaveInfoStore();
        var service = CreateService(store);

        await service.SetCultureAsync("zh-CN");

        Assert.Equal("易损件防呆管控系统", service.Catalog.MainWindow.Title);
        Assert.Equal(new[]
        {
            "易损件更换",
            "设备基础信息",
            "易损件管理",
            "换刀类型管理",
            "易损件更换历史",
            "用户配置"
        }, service.Catalog.MainWindow.Tabs);
    }

    [Fact]
    public async Task InitializeAsync_ShouldRestoreSavedCulture()
    {
        var store = new FakeSaveInfoStore();
        await store.WriteAsync(new LocalizationOptionsSaveInfo { CultureName = "zh-CN" });

        var service = CreateService(store);
        await service.InitializeAsync();

        Assert.Equal("zh-CN", service.CurrentCulture.Name);
        Assert.Equal("提示", service["FriendlyErrorTitle"]);
    }

    [Fact]
    public async Task InitializeAsync_ShouldPreferUserConfigLanguage()
    {
        var store = new FakeSaveInfoStore();
        await store.WriteAsync(new UserConfig { Language = "en-US" });
        await store.WriteAsync(new LocalizationOptionsSaveInfo { CultureName = "zh-CN" });
        await store.WriteAsync(new InstallationOptionsSaveInfo { CultureName = "zh-CN" });

        var service = CreateService(store);
        await service.InitializeAsync();

        Assert.Equal("en-US", service.CurrentCulture.Name);
        var persistedUserConfig = await store.ReadAsync<UserConfig>();
        Assert.Equal("en-US", persistedUserConfig.Language);
    }

    [Fact]
    public async Task InitializeAsync_WhenInstallerCultureExists_ShouldUseInstallerCultureOnFirstRun()
    {
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
        var store = new FakeSaveInfoStore();
        await store.WriteAsync(new InstallationOptionsSaveInfo { CultureName = "zh-CN" });

        var service = CreateService(store);
        await service.InitializeAsync();

        Assert.Equal("zh-CN", service.CurrentCulture.Name);
        var persistedUserConfig = await store.ReadAsync<UserConfig>();
        Assert.Equal("zh-CN", persistedUserConfig.Language);
    }

    [Fact]
    public async Task InitializeAsync_WhenInstallerCultureMissing_ShouldUseSupportedSystemCultureOnFirstRun()
    {
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("zh-CN");
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("zh-CN");
        var store = new FakeSaveInfoStore();

        var service = CreateService(store);
        await service.InitializeAsync();

        Assert.Equal("zh-CN", service.CurrentCulture.Name);
        var persistedUserConfig = await store.ReadAsync<UserConfig>();
        Assert.Equal("zh-CN", persistedUserConfig.Language);
    }

    [Fact]
    public async Task InitializeAsync_WhenSystemCultureUnsupported_ShouldFallbackToEnglishOnFirstRun()
    {
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("fr-FR");
        var store = new FakeSaveInfoStore();

        var service = CreateService(store);
        await service.InitializeAsync();

        Assert.Equal("en-US", service.CurrentCulture.Name);
        var persistedUserConfig = await store.ReadAsync<UserConfig>();
        Assert.Equal("en-US", persistedUserConfig.Language);
    }

    [Fact]
    public async Task SetCultureAsync_ShouldUpdateUserConfigLanguage()
    {
        var store = new FakeSaveInfoStore();
        var service = CreateService(store);

        await service.SetCultureAsync("en-US");

        var persistedUserConfig = await store.ReadAsync<UserConfig>();
        Assert.Equal("en-US", persistedUserConfig.Language);
        Assert.False(store.HasStored<LocalizationOptionsSaveInfo>());
    }

    [Fact]
    public async Task Indexer_ShouldFallbackToKey_WhenResourceMissing()
    {
        var store = new FakeSaveInfoStore();
        var service = CreateService(store);

        await service.SetCultureAsync("en-US");

        Assert.Equal("Missing.Key", service["Missing.Key"]);
    }

    [Fact]
    public async Task InitializeAsync_ShouldUseNormalizedLanguageFromUserConfigService()
    {
        var store = new FakeSaveInfoStore();
        await store.WriteAsync(new UserConfig { Language = " en-US " });

        var service = CreateService(store);

        await service.InitializeAsync();

        Assert.Equal("en-US", service.CurrentCulture.Name);
        var persistedUserConfig = await store.ReadAsync<UserConfig>();
        Assert.Equal("en-US", persistedUserConfig.Language);
    }

    [Fact]
    public async Task Indexer_ShouldReturnEnglish_WhenCalledFromNonUiThreadWithChineseThreadCulture()
    {
        var store = new FakeSaveInfoStore();
        var service = CreateService(store);

        await service.SetCultureAsync("en-US");

        var title = await Task.Run(() =>
        {
            var chineseCulture = CultureInfo.GetCultureInfo("zh-CN");
            CultureInfo.CurrentCulture = chineseCulture;
            CultureInfo.CurrentUICulture = chineseCulture;
            return service["MainWindow.Title"];
        });

        Assert.Equal("en-US", service.CurrentCulture.Name);
        Assert.Equal("WearPartsControl", title);
    }

    [Fact]
    public async Task LocalizedText_ShouldReturnEnglish_WhenCalledFromNonUiThreadWithChineseThreadCulture()
    {
        var store = new FakeSaveInfoStore();
        var service = CreateService(store);

        await service.SetCultureAsync("en-US");

        var title = await Task.Run(() =>
        {
            var chineseCulture = CultureInfo.GetCultureInfo("zh-CN");
            CultureInfo.CurrentCulture = chineseCulture;
            CultureInfo.CurrentUICulture = chineseCulture;
            return LocalizedText.Get("MainWindow.Title");
        });

        Assert.Equal("en-US", LocalizedText.CurrentCulture.Name);
        Assert.Equal("WearPartsControl", title);
    }

    public void Dispose()
    {
        CultureInfo.CurrentCulture = _originalCurrentCulture;
        CultureInfo.CurrentUICulture = _originalCurrentUiCulture;
        CultureInfo.DefaultThreadCurrentCulture = _originalDefaultThreadCurrentCulture;
        CultureInfo.DefaultThreadCurrentUICulture = _originalDefaultThreadCurrentUiCulture;
        LocalizedText.SetCulture(_originalCurrentUiCulture);
    }

    private static LocalizationService CreateService(FakeSaveInfoStore store)
    {
        return new LocalizationService(store, new UserConfigService(store));
    }

    private sealed class FakeSaveInfoStore : ISaveInfoStore
    {
        private readonly Dictionary<Type, object> _storage = new();

        public bool HasStored<T>() where T : class, new() => _storage.ContainsKey(typeof(T));

        public ValueTask<T> ReadAsync<T>(CancellationToken cancellationToken = default) where T : class, new()
        {
            if (_storage.TryGetValue(typeof(T), out var value))
            {
                return ValueTask.FromResult((T)value);
            }

            return ValueTask.FromResult(new T());
        }

        public ValueTask WriteAsync<T>(T model, CancellationToken cancellationToken = default) where T : class, new()
        {
            _storage[typeof(T)] = model;
            return ValueTask.CompletedTask;
        }
    }
}