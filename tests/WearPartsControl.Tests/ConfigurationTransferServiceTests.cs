using System.IO;
using System.IO.Compression;
using WearPartsControl.ApplicationServices.AppSettings;
using WearPartsControl.ApplicationServices.ConfigurationTransfer;
using WearPartsControl.ApplicationServices.SaveInfoService;
using WearPartsControl.Exceptions;
using Xunit;

namespace WearPartsControl.Tests;

public sealed class ConfigurationTransferServiceTests
{
    [Fact]
    public async Task ExportAsync_ShouldCreateCfgPackageWithCurrentConfigurationFiles()
    {
        var rootDirectory = CreateTempDirectory();
        try
        {
            var settingsDirectory = Path.Combine(rootDirectory, "Settings");
            var databaseDirectory = Path.Combine(rootDirectory, "LocalDB");
            Directory.CreateDirectory(settingsDirectory);
            Directory.CreateDirectory(databaseDirectory);
            await File.WriteAllTextAsync(Path.Combine(settingsDirectory, "user-config.json"), "{\"Language\":\"zh-CN\"}");
            await File.WriteAllTextAsync(Path.Combine(settingsDirectory, "skip.tmp"), "temporary");
            await File.WriteAllTextAsync(Path.Combine(settingsDirectory, "skip.log"), "log");
            await File.WriteAllTextAsync(Path.Combine(databaseDirectory, "wear-parts-control.db"), "db-content");

            var packagePath = Path.Combine(rootDirectory, "config.cfg");
            var service = CreateService(rootDirectory);

            var summary = await service.ExportAsync(packagePath);

            Assert.True(File.Exists(packagePath));
            Assert.Equal(packagePath, summary.PackagePath);
            Assert.Equal(2, summary.FileCount);

            using var archive = ZipFile.OpenRead(packagePath);
            var entryNames = archive.Entries.Select(static entry => entry.FullName).ToArray();
            Assert.Contains("configuration-package.json", entryNames);
            Assert.Contains("Settings/user-config.json", entryNames);
            Assert.Contains("LocalDB/wear-parts-control.db", entryNames);
            Assert.DoesNotContain("Settings/skip.tmp", entryNames);
            Assert.DoesNotContain("Settings/skip.log", entryNames);
        }
        finally
        {
            DeleteDirectory(rootDirectory);
        }
    }

    [Fact]
    public async Task ExportAsync_WhenPackageIsUnderIncludedRoot_ShouldNotIncludeExistingPackageFile()
    {
        var rootDirectory = CreateTempDirectory();
        try
        {
            var settingsDirectory = Path.Combine(rootDirectory, "Settings");
            Directory.CreateDirectory(settingsDirectory);
            await File.WriteAllTextAsync(Path.Combine(settingsDirectory, "user-config.json"), "{\"Language\":\"zh-CN\"}");
            var packagePath = Path.Combine(settingsDirectory, "config.cfg");
            await File.WriteAllTextAsync(packagePath, "old-package");

            var summary = await CreateService(rootDirectory).ExportAsync(packagePath);

            Assert.Equal(1, summary.FileCount);
            using var archive = ZipFile.OpenRead(packagePath);
            var entryNames = archive.Entries.Select(static entry => entry.FullName).ToArray();
            Assert.Contains("Settings/user-config.json", entryNames);
            Assert.DoesNotContain("Settings/config.cfg", entryNames);
        }
        finally
        {
            DeleteDirectory(rootDirectory);
        }
    }

    [Fact]
    public async Task ImportAsync_WhenClientInfoConfigured_ShouldRejectPackageBeforeReplacingFiles()
    {
        var rootDirectory = CreateTempDirectory();
        try
        {
            var packagePath = Path.Combine(rootDirectory, "config.cfg");
            await File.WriteAllTextAsync(packagePath, "not a zip");

            var settingsDirectory = Path.Combine(rootDirectory, "Settings");
            var appSettingsService = new AppSettingsService(new TypeJsonSaveInfoStore(settingsDirectory), settingsDirectory);
            await appSettingsService.SaveAsync(new AppSettings
            {
                ResourceNumber = "RES-01",
                IsSetClientAppInfo = true
            });

            var service = new ConfigurationTransferService(appSettingsService, rootDirectory);

            await Assert.ThrowsAsync<UserFriendlyException>(() => service.ImportAsync(packagePath));
        }
        finally
        {
            DeleteDirectory(rootDirectory);
        }
    }

    [Fact]
    public async Task ImportAsync_WhenClientInfoNotConfigured_ShouldRestoreSettingsAndLocalDatabase()
    {
        var workspace = CreateTempDirectory();
        try
        {
            var sourceRoot = Path.Combine(workspace, "source");
            var sourceSettingsDirectory = Path.Combine(sourceRoot, "Settings");
            var sourceDatabaseDirectory = Path.Combine(sourceRoot, "LocalDB");
            Directory.CreateDirectory(sourceSettingsDirectory);
            Directory.CreateDirectory(sourceDatabaseDirectory);
            await File.WriteAllTextAsync(Path.Combine(sourceSettingsDirectory, "app-settings.json"), "{\"ResourceNumber\":\"RES-99\",\"IsSetClientAppInfo\":true}");
            await File.WriteAllTextAsync(Path.Combine(sourceSettingsDirectory, "user-config.json"), "{\"Language\":\"en-US\"}");
            await File.WriteAllTextAsync(Path.Combine(sourceDatabaseDirectory, "wear-parts-control.db"), "imported-db");

            var packagePath = Path.Combine(workspace, "config.cfg");
            await CreateService(sourceRoot).ExportAsync(packagePath);

            var targetRoot = Path.Combine(workspace, "target");
            var targetSettingsDirectory = Path.Combine(targetRoot, "Settings");
            var targetDatabaseDirectory = Path.Combine(targetRoot, "LocalDB");
            Directory.CreateDirectory(targetSettingsDirectory);
            Directory.CreateDirectory(targetDatabaseDirectory);
            await File.WriteAllTextAsync(Path.Combine(targetSettingsDirectory, "app-settings.json"), "{\"ResourceNumber\":\"\",\"IsSetClientAppInfo\":false}");
            await File.WriteAllTextAsync(Path.Combine(targetSettingsDirectory, "old.json"), "old-settings");
            await File.WriteAllTextAsync(Path.Combine(targetDatabaseDirectory, "old.db"), "old-db");

            var appSettingsService = new AppSettingsService(new TypeJsonSaveInfoStore(targetSettingsDirectory), targetSettingsDirectory);
            AppSettings? savedSettings = null;
            appSettingsService.SettingsSaved += (_, settings) => savedSettings = settings;
            var service = new ConfigurationTransferService(appSettingsService, targetRoot);

            var summary = await service.ImportAsync(packagePath);

            Assert.Equal(packagePath, summary.PackagePath);
            Assert.Equal(3, summary.FileCount);
            Assert.False(File.Exists(Path.Combine(targetSettingsDirectory, "old.json")));
            Assert.False(File.Exists(Path.Combine(targetDatabaseDirectory, "old.db")));
            Assert.Equal("{\"Language\":\"en-US\"}", await File.ReadAllTextAsync(Path.Combine(targetSettingsDirectory, "user-config.json")));
            Assert.Equal("imported-db", await File.ReadAllTextAsync(Path.Combine(targetDatabaseDirectory, "wear-parts-control.db")));
            Assert.NotNull(savedSettings);
            Assert.True(savedSettings!.IsSetClientAppInfo);
            Assert.Equal("RES-99", savedSettings.ResourceNumber);
        }
        finally
        {
            DeleteDirectory(workspace);
        }
    }

    [Fact]
    public async Task ImportAsync_WhenPackageRootCasingDiffers_ShouldRestoreToCanonicalDirectories()
    {
        var rootDirectory = CreateTempDirectory();
        try
        {
            var packagePath = Path.Combine(rootDirectory, "config.cfg");
            using (var archive = ZipFile.Open(packagePath, ZipArchiveMode.Create))
            {
                var manifestEntry = archive.CreateEntry("configuration-package.json");
                await using (var manifestStream = manifestEntry.Open())
                await using (var writer = new StreamWriter(manifestStream))
                {
                    await writer.WriteAsync("{\"FormatVersion\":1,\"ProductName\":\"WearPartsControl\",\"ProductVersion\":\"1.0.0\",\"ExportedAt\":\"2026-05-12T00:00:00+00:00\",\"IncludedRoots\":[\"Settings\",\"LocalDB\"]}");
                }

                var settingsEntry = archive.CreateEntry("settings/user-config.json");
                await using var settingsStream = settingsEntry.Open();
                await using var settingsWriter = new StreamWriter(settingsStream);
                await settingsWriter.WriteAsync("{\"Language\":\"en-US\"}");
            }

            var appSettingsService = new AppSettingsService(new TypeJsonSaveInfoStore(Path.Combine(rootDirectory, "Settings")), Path.Combine(rootDirectory, "Settings"));
            await appSettingsService.SaveAsync(new AppSettings());
            var service = new ConfigurationTransferService(appSettingsService, rootDirectory);

            await service.ImportAsync(packagePath);

            Assert.True(File.Exists(Path.Combine(rootDirectory, "Settings", "user-config.json")));
            Assert.DoesNotContain("settings", Directory.EnumerateDirectories(rootDirectory).Select(Path.GetFileName), StringComparer.Ordinal);
        }
        finally
        {
            DeleteDirectory(rootDirectory);
        }
    }

    private static ConfigurationTransferService CreateService(string rootDirectory)
    {
        var settingsDirectory = Path.Combine(rootDirectory, "Settings");
        var appSettingsService = new AppSettingsService(new TypeJsonSaveInfoStore(settingsDirectory), settingsDirectory);
        return new ConfigurationTransferService(appSettingsService, rootDirectory);
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"WearPartsControl.ConfigTransfer.Tests.{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }
}