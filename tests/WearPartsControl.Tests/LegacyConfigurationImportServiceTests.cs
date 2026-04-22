using System.IO;
using Microsoft.Data.Sqlite;
using WearPartsControl.ApplicationServices.AppSettings;
using WearPartsControl.ApplicationServices.ClientAppInfo;
using WearPartsControl.ApplicationServices.ComNotification;
using WearPartsControl.ApplicationServices.LegacyImport;
using WearPartsControl.ApplicationServices.LoginService;
using WearPartsControl.ApplicationServices.SaveInfoService;
using WearPartsControl.ApplicationServices.SpacerManagement;
using WearPartsControl.ApplicationServices.UserConfig;
using Xunit;

namespace WearPartsControl.Tests;

public sealed class LegacyConfigurationImportServiceTests
{
    [Fact]
    public async Task ImportAsync_ShouldConvertLegacyDatabaseAndJsonFiles()
    {
        var workspace = Path.Combine(Path.GetTempPath(), $"WearPartsControl.Tests.{Guid.NewGuid():N}");
        var legacyRoot = Path.Combine(workspace, "legacy");
        var jsonDirectory = Path.Combine(legacyRoot, "Json");
        var databaseDirectory = Path.Combine(legacyRoot, "db");
        Directory.CreateDirectory(jsonDirectory);
        Directory.CreateDirectory(databaseDirectory);

        try
        {
            var databasePath = Path.Combine(databaseDirectory, "Data.db");
            await CreateLegacyDatabaseAsync(databasePath);
            await File.WriteAllTextAsync(Path.Combine(jsonDirectory, "AppSetting.json"), """
{"ResourceNum":"RES-LEGACY"}
""");
            await File.WriteAllTextAsync(Path.Combine(jsonDirectory, "AppConfig.json"), """
{"UseUserNumber":true,"SpacerValidationUrl":"https://legacy/spacer","UserWorkId":"WU001","AccessToken":"TOKEN-001","Secret":"SECRET-001"}
""");
            await File.WriteAllTextAsync(Path.Combine(jsonDirectory, "MHRInfos.json"), """
{"LoginName":"mhr-user","Password":"mhr-pass","MHRInfos":[{"Site":"F1","LoginUrl":"https://mhr/login","GetUsersUrl":"https://mhr/users"}]}
""");

            var saveInfoStore = new TypeJsonSaveInfoStore(Path.Combine(workspace, "settings"));
            var appSettingsService = new AppSettingsService(saveInfoStore, Path.Combine(workspace, "settings"));
            var clientAppInfoService = new StubClientAppInfoService();
            var service = new LegacyConfigurationImportService(clientAppInfoService, appSettingsService, saveInfoStore);

            var result = await service.ImportAsync(databasePath);

            Assert.Equal(databasePath, result.LegacyDatabasePath);
            Assert.Equal("RES-LEGACY", result.ResourceNumber);
            Assert.NotNull(clientAppInfoService.LastRequest);
            Assert.Equal("SITE-01", clientAppInfoService.LastRequest!.SiteCode);
            Assert.Equal("RES-LEGACY", clientAppInfoService.LastRequest.ResourceNumber);
            Assert.Equal("192.168.1.100", clientAppInfoService.LastRequest.PlcIpAddress);
            Assert.True(clientAppInfoService.LastRequest.IsStringReverse);

            var appSettings = await appSettingsService.GetAsync();
            Assert.Equal("RES-LEGACY", appSettings.ResourceNumber);
            Assert.True(appSettings.UseWorkNumberLogin);
            Assert.True(appSettings.IsSetClientAppInfo);

            var userConfig = await saveInfoStore.ReadAsync<UserConfig>();
            Assert.Equal("WU001", userConfig.MeResponsibleWorkId);
            Assert.Equal("TOKEN-001", userConfig.ComAccessToken);
            Assert.Equal("SECRET-001", userConfig.ComSecret);

            var comNotification = await saveInfoStore.ReadAsync<ComNotificationOptionsSaveInfo>();
            Assert.Equal("TOKEN-001", comNotification.AccessToken);
            Assert.Equal("SECRET-001", comNotification.Secret);
            Assert.Equal("WU001", comNotification.DefaultUserWorkId);

            var spacerValidation = await saveInfoStore.ReadAsync<SpacerValidationOptionsSaveInfo>();
            Assert.True(spacerValidation.Enabled);
            Assert.Equal("https://legacy/spacer", spacerValidation.ValidationUrl);

            var mhrConfig = await saveInfoStore.ReadAsync<MhrConfig>();
            Assert.Equal("mhr-user", mhrConfig.LoginName);
            Assert.Equal("mhr-pass", mhrConfig.Password);
            Assert.Single(mhrConfig.LoginInfos);
            Assert.Equal("F1", mhrConfig.LoginInfos[0].Site);
        }
        finally
        {
            if (Directory.Exists(workspace))
            {
                try
                {
                    Directory.Delete(workspace, true);
                }
                catch (IOException)
                {
                }
            }
        }
    }

    private static async Task CreateLegacyDatabaseAsync(string databasePath)
    {
        await using var connection = new SqliteConnection($"Data Source={databasePath}");
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = """
CREATE TABLE v_Basic (
    Id TEXT NOT NULL,
    Site TEXT,
    Factory TEXT,
    Area TEXT,
    Procedure TEXT,
    EquipmentNum TEXT,
    ResourceNum TEXT,
    PlcType TEXT,
    PlcIp TEXT,
    Port INTEGER,
    ShutdownPoint TEXT,
    SiemensSlot INTEGER,
    IsStringReverse INTEGER
);
INSERT INTO v_Basic (Id, Site, Factory, Area, Procedure, EquipmentNum, ResourceNum, PlcType, PlcIp, Port, ShutdownPoint, SiemensSlot, IsStringReverse)
VALUES ('1', 'SITE-01', 'FACTORY-01', 'AREA-01', 'PROC-01', 'EQ-01', 'RES-LEGACY', 'ModbusTcp', '192.168.1.100', 502, 'M100.0', 0, 1);
""";
        await command.ExecuteNonQueryAsync();
    }

    private sealed class StubClientAppInfoService : IClientAppInfoService
    {
        public ClientAppInfoSaveRequest? LastRequest { get; private set; }

        public Task<ClientAppInfoModel> GetAsync(CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<ClientAppInfoModel> SaveAsync(ClientAppInfoSaveRequest request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult(new ClientAppInfoModel
            {
                Id = Guid.NewGuid(),
                SiteCode = request.SiteCode,
                FactoryCode = request.FactoryCode,
                AreaCode = request.AreaCode,
                ProcedureCode = request.ProcedureCode,
                EquipmentCode = request.EquipmentCode,
                ResourceNumber = request.ResourceNumber,
                PlcProtocolType = request.PlcProtocolType,
                PlcIpAddress = request.PlcIpAddress,
                PlcPort = request.PlcPort,
                ShutdownPointAddress = request.ShutdownPointAddress,
                SiemensRack = request.SiemensRack,
                SiemensSlot = request.SiemensSlot,
                IsStringReverse = request.IsStringReverse
            });
        }
    }
}