using System;
using System.IO;
using System.Threading.Tasks;
using WearPartsControl.ApplicationServices.SaveInfoService;
using Xunit;

namespace WearPartsControl.Tests;

public sealed class TypeJsonSaveInfoStoreTests
{
    [Fact]
    public async Task WriteReadAsync_ShouldPersistData_AndAutoMapToJsonByTypeName()
    {
        var root = CreateIsolatedRoot();
        var store = new TypeJsonSaveInfoStore(root);

        var model = new AutoMappedSaveInfo
        {
            Name = "Alice",
            Counter = 7
        };

        await store.WriteAsync(model);
        var loaded = await store.ReadAsync<AutoMappedSaveInfo>();

        Assert.Equal("Alice", loaded.Name);
        Assert.Equal(7, loaded.Counter);

        var expectedFile = Path.Combine(root, typeof(AutoMappedSaveInfo).FullName + ".json");
        Assert.True(File.Exists(expectedFile));
    }

    [Fact]
    public async Task WriteReadAsync_ShouldUseAttributeMappedFile()
    {
        var root = CreateIsolatedRoot();
        var store = new TypeJsonSaveInfoStore(root);

        await store.WriteAsync(new MappedSaveInfo
        {
            Enabled = true
        });

        var loaded = await store.ReadAsync<MappedSaveInfo>();
        Assert.True(loaded.Enabled);

        var expectedFile = Path.Combine(root, "settings", "custom-saveinfo.json");
        Assert.True(File.Exists(expectedFile));
    }

    [Fact]
    public async Task ReadAsync_WhenFileMissing_ShouldReturnNewInstance()
    {
        var root = CreateIsolatedRoot();
        var store = new TypeJsonSaveInfoStore(root);

        var loaded = await store.ReadAsync<AutoMappedSaveInfo>();

        Assert.NotNull(loaded);
        Assert.Equal(string.Empty, loaded.Name);
        Assert.Equal(0, loaded.Counter);
    }

    [Fact]
    public async Task WriteAsync_WhenPathTraversalConfigured_ShouldThrow()
    {
        var root = CreateIsolatedRoot();
        var store = new TypeJsonSaveInfoStore(root);

        await Assert.ThrowsAsync<InvalidOperationException>(() => store.WriteAsync(new UnsafeSaveInfo()).AsTask());
    }

    private static string CreateIsolatedRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "WearPartsControl.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private sealed class AutoMappedSaveInfo
    {
        public string Name { get; set; } = string.Empty;

        public int Counter { get; set; }
    }

    [SaveInfoFile("custom-saveinfo", BaseDirectory = "settings")]
    private sealed class MappedSaveInfo
    {
        public bool Enabled { get; set; }
    }

    [SaveInfoFile("../unsafe")]
    private sealed class UnsafeSaveInfo
    {
        public int Value { get; set; }
    }
}
