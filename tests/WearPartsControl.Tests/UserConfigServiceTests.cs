using System.IO;
using System.Text.Json;
using WearPartsControl.ApplicationServices.SaveInfoService;
using WearPartsControl.ApplicationServices.SpacerManagement;
using WearPartsControl.ApplicationServices.UserConfig;
using Xunit;

namespace WearPartsControl.Tests;

public sealed class UserConfigServiceTests
{
    [Fact]
    public async Task SaveAsync_ShouldPersistNormalizedUserConfig()
    {
        var settingsDirectory = Path.Combine(Path.GetTempPath(), $"WearPartsControl.UserConfig.{Guid.NewGuid():N}");
        Directory.CreateDirectory(settingsDirectory);

        try
        {
            var service = new UserConfigService(new TypeJsonSaveInfoStore(settingsDirectory));

            await service.SaveAsync(new UserConfig
            {
                MeResponsibleWorkId = "  ME001  ",
                PrdResponsibleWorkId = " PRD001 ",
                ComAccessToken = " token ",
                ComSecret = " secret ",
                SpacerValidationEnabled = false,
                SpacerValidationUrl = " https://spacer/save ",
                SpacerValidationTimeoutMilliseconds = 7000,
                SpacerValidationIgnoreServerCertificateErrors = false,
                SpacerValidationCodeSeparator = " - ",
                SpacerValidationExpectedSegmentCount = 9
            });

            var config = await service.GetAsync();
            var json = await File.ReadAllTextAsync(Path.Combine(settingsDirectory, "user-config.json"));
            var persisted = JsonSerializer.Deserialize<UserConfig>(json);

            Assert.Equal("ME001", config.MeResponsibleWorkId);
            Assert.Equal("PRD001", config.PrdResponsibleWorkId);
            Assert.Equal("token", config.ComAccessToken);
            Assert.Equal("secret", config.ComSecret);
            Assert.False(config.SpacerValidationEnabled);
            Assert.Equal("https://spacer/save", config.SpacerValidationUrl);
            Assert.Equal(7000, config.SpacerValidationTimeoutMilliseconds);
            Assert.False(config.SpacerValidationIgnoreServerCertificateErrors);
            Assert.Equal("-", config.SpacerValidationCodeSeparator);
            Assert.Equal(9, config.SpacerValidationExpectedSegmentCount);
            Assert.NotNull(persisted);
            Assert.Equal("ME001", persisted!.MeResponsibleWorkId);
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
    public async Task GetAsync_WhenLegacySpacerValidationExists_ShouldMigrateIntoUserConfig()
    {
        var settingsDirectory = Path.Combine(Path.GetTempPath(), $"WearPartsControl.UserConfig.{Guid.NewGuid():N}");
        Directory.CreateDirectory(settingsDirectory);

        try
        {
            var store = new TypeJsonSaveInfoStore(settingsDirectory);
            await store.WriteAsync(new SpacerValidationOptionsSaveInfo
            {
                Enabled = false,
                ValidationUrl = "https://legacy/spacer",
                TimeoutMilliseconds = 6000,
                IgnoreServerCertificateErrors = false,
                CodeSeparator = "-",
                ExpectedSegmentCount = 10
            });

            var service = new UserConfigService(store);

            var config = await service.GetAsync();

            Assert.False(config.SpacerValidationEnabled);
            Assert.Equal("https://legacy/spacer", config.SpacerValidationUrl);
            Assert.Equal(6000, config.SpacerValidationTimeoutMilliseconds);
            Assert.False(config.SpacerValidationIgnoreServerCertificateErrors);
            Assert.Equal("-", config.SpacerValidationCodeSeparator);
            Assert.Equal(10, config.SpacerValidationExpectedSegmentCount);
            Assert.False(store.Exists<SpacerValidationOptionsSaveInfo>());

            var persistedJson = await File.ReadAllTextAsync(Path.Combine(settingsDirectory, "user-config.json"));
            var persisted = JsonSerializer.Deserialize<UserConfig>(persistedJson);
            Assert.NotNull(persisted);
            Assert.Equal("https://legacy/spacer", persisted!.SpacerValidationUrl);
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