using System.IO;
using System.Text.Json;
using WearPartsControl.ApplicationServices.SaveInfoService;
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
                ComSecret = " secret "
            });

            var config = await service.GetAsync();
            var json = await File.ReadAllTextAsync(Path.Combine(settingsDirectory, "user-config.json"));
            var persisted = JsonSerializer.Deserialize<UserConfig>(json);

            Assert.Equal("ME001", config.MeResponsibleWorkId);
            Assert.Equal("PRD001", config.PrdResponsibleWorkId);
            Assert.Equal("token", config.ComAccessToken);
            Assert.Equal("secret", config.ComSecret);
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
}