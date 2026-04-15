using System.Globalization;
using WearPartsControl.ApplicationServices.Localization;
using WearPartsControl.ApplicationServices.SaveInfoService;
using Xunit;

namespace WearPartsControl.Tests;

public sealed class LocalizationServiceTests
{
    [Fact]
    public async Task Catalog_ShouldReturnStructuredTabs_ForEnglishCulture()
    {
        var store = new FakeSaveInfoStore();
        var service = new LocalizationService(store);

        await service.SetCultureAsync("en-US");

        Assert.Equal("WearPartsControl", service.Catalog.MainWindow.Title);
        Assert.Equal(new[]
        {
            "Wear Part Replacement",
            "Equipment Information",
            "Wear Part Management",
            "Replacement History",
            "User Settings"
        }, service.Catalog.MainWindow.Tabs);
    }

    [Fact]
    public async Task Catalog_ShouldReturnStructuredTabs_ForChineseCulture()
    {
        var store = new FakeSaveInfoStore();
        var service = new LocalizationService(store);

        await service.SetCultureAsync("zh-CN");

        Assert.Equal("易损件防呆管控系统", service.Catalog.MainWindow.Title);
        Assert.Equal(new[]
        {
            "易损件更换",
            "设备基础信息",
            "易损件管理",
            "易损件更换历史",
            "用户配置"
        }, service.Catalog.MainWindow.Tabs);
    }

    [Fact]
    public async Task InitializeAsync_ShouldRestoreSavedCulture()
    {
        var store = new FakeSaveInfoStore();
        await store.WriteAsync(new LocalizationOptionsSaveInfo { CultureName = "zh-CN" });

        var service = new LocalizationService(store);
        await service.InitializeAsync();

        Assert.Equal("zh-CN", service.CurrentCulture.Name);
        Assert.Equal("提示", service["FriendlyErrorTitle"]);
    }

    [Fact]
    public async Task Indexer_ShouldFallbackToKey_WhenResourceMissing()
    {
        var store = new FakeSaveInfoStore();
        var service = new LocalizationService(store);

        await service.SetCultureAsync("en-US");

        Assert.Equal("Missing.Key", service["Missing.Key"]);
    }

    private sealed class FakeSaveInfoStore : ISaveInfoStore
    {
        private readonly Dictionary<Type, object> _storage = new();

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