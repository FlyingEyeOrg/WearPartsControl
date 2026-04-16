using System.IO;
using System.Text.Json;
using WearPartsControl.ApplicationServices.AppSettings;
using WearPartsControl.ApplicationServices.SaveInfoService;
using Xunit;

namespace WearPartsControl.Tests;

public sealed class AppSettingsServiceTests
{
    [Fact]
    public async Task GetAsync_ShouldMigrateLegacyAppSettingFile()
    {
        var settingsDirectory = Path.Combine(Path.GetTempPath(), $"WearPartsControl.Tests.{Guid.NewGuid():N}");
        Directory.CreateDirectory(settingsDirectory);

        try
        {
            var legacyPath = Path.Combine(settingsDirectory, "app-setting.json");
            var legacySettings = new AppSettings
            {
                ResourceNumber = "RES-001",
                LoginInputMaxIntervalMilliseconds = 135
            };

            await File.WriteAllTextAsync(legacyPath, JsonSerializer.Serialize(legacySettings));

            var store = new TypeJsonSaveInfoStore(settingsDirectory);
            var service = new AppSettingsService(store, settingsDirectory);

            var settings = await service.GetAsync();

            Assert.Equal("RES-001", settings.ResourceNumber);
            Assert.Equal(135, settings.LoginInputMaxIntervalMilliseconds);
            Assert.False(File.Exists(legacyPath));
            Assert.True(File.Exists(Path.Combine(settingsDirectory, "app-settings.json")));
        }
        finally
        {
            if (Directory.Exists(settingsDirectory))
            {
                Directory.Delete(settingsDirectory, true);
            }
        }
    }
}