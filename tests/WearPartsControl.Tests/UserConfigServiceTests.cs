using System.IO;
using System.Text.Json;
using WearPartsControl.ApplicationServices.ComNotification;
using WearPartsControl.ApplicationServices.Localization;
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
                MeResponsibleName = "  张三  ",
                PrdResponsibleWorkId = " PRD001 ",
                PrdResponsibleName = " 李四 ",
                ReplacementOperatorName = " 王五 ",
                Language = " en-US ",
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
            Assert.Equal("张三", config.MeResponsibleName);
            Assert.Equal("PRD001", config.PrdResponsibleWorkId);
            Assert.Equal("李四", config.PrdResponsibleName);
            Assert.Equal("王五", config.ReplacementOperatorName);
            Assert.Equal("en-US", config.Language);
            Assert.Equal("token", config.ComAccessToken);
            Assert.Equal("secret", config.ComSecret);
            Assert.True(config.ComNotificationEnabled);
            Assert.Equal(UserConfig.DefaultComPushUrl, config.ComPushUrl);
            Assert.Equal(UserConfig.DefaultComDeIpaasKeyAuth, config.ComDeIpaasKeyAuth);
            Assert.Equal(UserConfig.DefaultComAgentId, config.ComAgentId);
            Assert.Equal(UserConfig.DefaultComGroupTemplateId, config.ComGroupTemplateId);
            Assert.Equal(UserConfig.DefaultComWorkTemplateId, config.ComWorkTemplateId);
            Assert.Equal(UserConfig.DefaultComUserType, config.ComUserType);
            Assert.Equal(UserConfig.DefaultComTimeoutMilliseconds, config.ComTimeoutMilliseconds);
            Assert.False(config.SpacerValidationEnabled);
            Assert.Equal("https://spacer/save", config.SpacerValidationUrl);
            Assert.Equal(7000, config.SpacerValidationTimeoutMilliseconds);
            Assert.False(config.SpacerValidationIgnoreServerCertificateErrors);
            Assert.Equal("-", config.SpacerValidationCodeSeparator);
            Assert.Equal(9, config.SpacerValidationExpectedSegmentCount);
            Assert.NotNull(persisted);
            Assert.Equal("ME001", persisted!.MeResponsibleWorkId);
            Assert.Equal("张三", persisted.MeResponsibleName);
            Assert.Equal("en-US", persisted.Language);
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

    [Fact]
    public async Task GetAsync_WhenLegacyComNotificationExists_ShouldMigrateIntoUserConfig()
    {
        var settingsDirectory = Path.Combine(Path.GetTempPath(), $"WearPartsControl.UserConfig.{Guid.NewGuid():N}");
        Directory.CreateDirectory(settingsDirectory);

        try
        {
            var store = new TypeJsonSaveInfoStore(settingsDirectory);
            await store.WriteAsync(new ComNotificationOptionsSaveInfo
            {
                Enabled = true,
                PushUrl = "https://legacy/com",
                DeIpaasKeyAuth = "legacy-auth",
                AgentId = 11,
                GroupTemplateId = 22,
                WorkTemplateId = 33,
                UserType = "ding",
                AccessToken = "token",
                Secret = "secret",
                DefaultUserWorkId = "ME001",
                TimeoutMilliseconds = 12000
            });

            var service = new UserConfigService(store);

            var config = await service.GetAsync();

            Assert.True(config.ComNotificationEnabled);
            Assert.Equal("https://legacy/com", config.ComPushUrl);
            Assert.Equal("legacy-auth", config.ComDeIpaasKeyAuth);
            Assert.Equal(11, config.ComAgentId);
            Assert.Equal(22, config.ComGroupTemplateId);
            Assert.Equal(33, config.ComWorkTemplateId);
            Assert.Equal("ding", config.ComUserType);
            Assert.Equal("token", config.ComAccessToken);
            Assert.Equal("secret", config.ComSecret);
            Assert.Equal("ME001", config.MeResponsibleWorkId);
            Assert.Equal(12000, config.ComTimeoutMilliseconds);
            Assert.False(store.Exists<ComNotificationOptionsSaveInfo>());

            var persistedJson = await File.ReadAllTextAsync(Path.Combine(settingsDirectory, "user-config.json"));
            var persisted = JsonSerializer.Deserialize<UserConfig>(persistedJson);
            Assert.NotNull(persisted);
            Assert.Equal("https://legacy/com", persisted!.ComPushUrl);
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
    public async Task SaveAsync_WhenComNotificationDisabled_ShouldPersistFalseValue()
    {
        var settingsDirectory = Path.Combine(Path.GetTempPath(), $"WearPartsControl.UserConfig.{Guid.NewGuid():N}");
        Directory.CreateDirectory(settingsDirectory);

        try
        {
            var service = new UserConfigService(new TypeJsonSaveInfoStore(settingsDirectory));

            await service.SaveAsync(new UserConfig
            {
                ComNotificationEnabled = false
            });

            var config = await service.GetAsync();

            Assert.False(config.ComNotificationEnabled);
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
    public async Task GetAsync_WhenLegacyLocalizationExists_ShouldMigrateLanguageIntoUserConfig()
    {
        var settingsDirectory = Path.Combine(Path.GetTempPath(), $"WearPartsControl.UserConfig.{Guid.NewGuid():N}");
        Directory.CreateDirectory(settingsDirectory);

        try
        {
            var store = new TypeJsonSaveInfoStore(settingsDirectory);
            await store.WriteAsync(new LocalizationOptionsSaveInfo { CultureName = "en-US" });

            var service = new UserConfigService(store);

            var config = await service.GetAsync();

            Assert.Equal("en-US", config.Language);
            Assert.False(store.Exists<LocalizationOptionsSaveInfo>());

            var persistedJson = await File.ReadAllTextAsync(Path.Combine(settingsDirectory, "user-config.json"));
            var persisted = JsonSerializer.Deserialize<UserConfig>(persistedJson);
            Assert.NotNull(persisted);
            Assert.Equal("en-US", persisted!.Language);
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