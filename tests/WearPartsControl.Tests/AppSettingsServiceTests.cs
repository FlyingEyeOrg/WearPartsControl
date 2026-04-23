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
                LoginInputMaxIntervalMilliseconds = 135,
                AutoLogoutCountdownSeconds = 240,
                UseWorkNumberLogin = true,
                IsWearPartMonitoringEnabled = false
            };

            await File.WriteAllTextAsync(legacyPath, JsonSerializer.Serialize(legacySettings));

            var store = new TypeJsonSaveInfoStore(settingsDirectory);
            var service = new AppSettingsService(store, settingsDirectory);

            var settings = await service.GetAsync();

            Assert.Equal("RES-001", settings.ResourceNumber);
            Assert.Equal(135, settings.LoginInputMaxIntervalMilliseconds);
            Assert.Equal(240, settings.AutoLogoutCountdownSeconds);
            Assert.True(settings.UseWorkNumberLogin);
            Assert.False(settings.IsWearPartMonitoringEnabled);
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

    [Fact]
    public async Task GetAsync_ShouldNormalizeInvalidPlcPipelineThresholds()
    {
        var settingsDirectory = Path.Combine(Path.GetTempPath(), $"WearPartsControl.Tests.{Guid.NewGuid():N}");
        Directory.CreateDirectory(settingsDirectory);

        try
        {
            var store = new TypeJsonSaveInfoStore(settingsDirectory);
            await store.WriteAsync(new AppSettings
            {
                PlcPipeline = new PlcPipelineSettings
                {
                    SlowQueueWaitThresholdMilliseconds = 0,
                    SlowExecutionThresholdMilliseconds = -1
                }
            });

            var service = new AppSettingsService(store, settingsDirectory);

            var settings = await service.GetAsync();

            Assert.Equal(100, settings.PlcPipeline.SlowQueueWaitThresholdMilliseconds);
            Assert.Equal(500, settings.PlcPipeline.SlowExecutionThresholdMilliseconds);
        }
        finally
        {
            if (Directory.Exists(settingsDirectory))
            {
                Directory.Delete(settingsDirectory, true);
            }
        }
    }

    [Fact]
    public async Task GetAsync_WhenMonitoringFlagMissing_ShouldDefaultToDisabled()
    {
        var settingsDirectory = Path.Combine(Path.GetTempPath(), $"WearPartsControl.Tests.{Guid.NewGuid():N}");
        Directory.CreateDirectory(settingsDirectory);

        try
        {
            var path = Path.Combine(settingsDirectory, "app-settings.json");
            await File.WriteAllTextAsync(path, """
{
  "ResourceNumber": "",
  "LoginInputMaxIntervalMilliseconds": 2000,
  "PlcPipeline": {
    "SlowQueueWaitThresholdMilliseconds": 100,
    "SlowExecutionThresholdMilliseconds": 500
  },
  "IsSetClientAppInfo": false
}
""");

            var store = new TypeJsonSaveInfoStore(settingsDirectory);
            var service = new AppSettingsService(store, settingsDirectory);

            var settings = await service.GetAsync();

            Assert.False(settings.IsWearPartMonitoringEnabled);
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