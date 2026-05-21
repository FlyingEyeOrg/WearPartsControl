using System.IO;
using System.IO.Compression;
using Microsoft.Data.Sqlite;
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
    public async Task ImportAsync_WhenClientInfoConfigured_ShouldPreserveCurrentClientInfoAndRebindImportedData()
    {
        var workspace = CreateTempDirectory();
        try
        {
            var sourceRoot = Path.Combine(workspace, "source");
            var sourceSettingsDirectory = Path.Combine(sourceRoot, "Settings");
            var sourceDatabaseDirectory = Path.Combine(sourceRoot, "LocalDB");
            Directory.CreateDirectory(sourceSettingsDirectory);
            Directory.CreateDirectory(sourceDatabaseDirectory);

            var sourceAppSettingsService = CreateAppSettingsService(sourceSettingsDirectory);
            await sourceAppSettingsService.SaveAsync(new AppSettings
            {
                ResourceNumber = "SRC-01",
                IsSetClientAppInfo = true,
                AutoLogoutCountdownSeconds = 720,
                UseWorkNumberLogin = true
            });
            await File.WriteAllTextAsync(Path.Combine(sourceSettingsDirectory, "user-config.json"), "{\"Language\":\"en-US\"}");
            await CreateSqliteDatabaseWithClientConfigurationAsync(
                Path.Combine(sourceDatabaseDirectory, "wear-parts-control.db"),
                "imported-db",
                new TestClientAppConfiguration(
                    "source-config",
                    "SITE-SRC",
                    "FACT-SRC",
                    "AREA-SRC",
                    "PROC-SRC",
                    "EQ-SRC",
                    "SRC-01",
                    "SiemensS7",
                    "192.168.0.10",
                    102,
                    "DB1.DBX0.0",
                    false,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    0,
                    1,
                    false,
                    string.Empty),
                new TestWearPartDefinition("wear-part-1", "source-config", "SRC-01", "WP-01"));

            var packagePath = Path.Combine(workspace, "config.cfg");
            await CreateService(sourceRoot).ExportAsync(packagePath);

            var targetRoot = Path.Combine(workspace, "target");
            var targetSettingsDirectory = Path.Combine(targetRoot, "Settings");
            var targetDatabaseDirectory = Path.Combine(targetRoot, "LocalDB");
            Directory.CreateDirectory(targetSettingsDirectory);
            Directory.CreateDirectory(targetDatabaseDirectory);

            var targetAppSettingsService = CreateAppSettingsService(targetSettingsDirectory);
            await targetAppSettingsService.SaveAsync(new AppSettings
            {
                ResourceNumber = "TARGET-01",
                IsSetClientAppInfo = true,
                AutoLogoutCountdownSeconds = 90,
                UseWorkNumberLogin = false
            });
            await File.WriteAllTextAsync(Path.Combine(targetSettingsDirectory, "old.json"), "old-settings");
            await CreateSqliteDatabaseWithClientConfigurationAsync(
                Path.Combine(targetDatabaseDirectory, "wear-parts-control.db"),
                "target-db",
                new TestClientAppConfiguration(
                    "target-config",
                    "SITE-TGT",
                    "FACT-TGT",
                    "AREA-TGT",
                    "PROC-TGT",
                    "EQ-TGT",
                    "TARGET-01",
                    "Mitsubishi",
                    "10.0.0.5",
                    9600,
                    "M100",
                    true,
                    "http://target/wsdl",
                    "target-user",
                    "target-password",
                    "target-site",
                    2,
                    3,
                    true,
                    string.Empty));

            var service = new ConfigurationTransferService(targetAppSettingsService, targetRoot);

            var summary = await service.ImportAsync(packagePath);

            Assert.Equal(packagePath, summary.PackagePath);
            Assert.Equal(3, summary.FileCount);
            Assert.False(File.Exists(Path.Combine(targetSettingsDirectory, "old.json")));
            Assert.Equal("{\"Language\":\"en-US\"}", await File.ReadAllTextAsync(Path.Combine(targetSettingsDirectory, "user-config.json")));
            Assert.Equal("imported-db", await ReadSqliteDatabasePayloadAsync(Path.Combine(targetDatabaseDirectory, "wear-parts-control.db")));

            var importedSettings = await targetAppSettingsService.GetAsync();
            Assert.True(importedSettings.IsSetClientAppInfo);
            Assert.Equal("TARGET-01", importedSettings.ResourceNumber);
            Assert.Equal(720, importedSettings.AutoLogoutCountdownSeconds);
            Assert.True(importedSettings.UseWorkNumberLogin);

            var preservedConfiguration = await ReadClientAppConfigurationAsync(Path.Combine(targetDatabaseDirectory, "wear-parts-control.db"), "TARGET-01");
            Assert.NotNull(preservedConfiguration);
            Assert.Equal("source-config", preservedConfiguration!.Id);
            Assert.Equal("SITE-TGT", preservedConfiguration.SiteCode);
            Assert.Equal("FACT-TGT", preservedConfiguration.FactoryCode);
            Assert.Equal("EQ-TGT", preservedConfiguration.EquipmentCode);
            Assert.Equal("Mitsubishi", preservedConfiguration.PlcProtocolType);
            Assert.Equal("10.0.0.5", preservedConfiguration.PlcIpAddress);
            Assert.Equal(9600, preservedConfiguration.PlcPort);
            Assert.True(preservedConfiguration.EnableCutterMesValidation);
            Assert.True(preservedConfiguration.IsStringReverse);

            Assert.Null(await ReadClientAppConfigurationAsync(Path.Combine(targetDatabaseDirectory, "wear-parts-control.db"), "SRC-01"));

            var importedWearPart = await ReadWearPartDefinitionAsync(Path.Combine(targetDatabaseDirectory, "wear-parts-control.db"), "WP-01");
            Assert.NotNull(importedWearPart);
            Assert.Equal("source-config", importedWearPart!.ClientAppConfigurationId);
            Assert.Equal("TARGET-01", importedWearPart.ResourceNumber);
        }
        finally
        {
            DeleteDirectory(workspace);
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
            await CreateSqliteDatabaseAsync(Path.Combine(sourceDatabaseDirectory, "wear-parts-control.db"), "imported-db");

            var packagePath = Path.Combine(workspace, "config.cfg");
            await CreateService(sourceRoot).ExportAsync(packagePath);

            var targetRoot = Path.Combine(workspace, "target");
            var targetSettingsDirectory = Path.Combine(targetRoot, "Settings");
            var targetDatabaseDirectory = Path.Combine(targetRoot, "LocalDB");
            Directory.CreateDirectory(targetSettingsDirectory);
            Directory.CreateDirectory(targetDatabaseDirectory);
            await File.WriteAllTextAsync(Path.Combine(targetSettingsDirectory, "app-settings.json"), "{\"ResourceNumber\":\"\",\"IsSetClientAppInfo\":false}");
            await File.WriteAllTextAsync(Path.Combine(targetSettingsDirectory, "old.json"), "old-settings");
            await CreateSqliteDatabaseAsync(Path.Combine(targetDatabaseDirectory, "wear-parts-control.db"), "old-db-content");
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
            Assert.Equal("imported-db", await ReadSqliteDatabasePayloadAsync(Path.Combine(targetDatabaseDirectory, "wear-parts-control.db")));
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

    [Fact]
    public async Task ImportAsync_WhenTargetDatabaseConnectionIsOpen_ShouldRestoreLocalDatabase()
    {
        var workspace = CreateTempDirectory();
        try
        {
            var sourceRoot = Path.Combine(workspace, "source");
            var sourceSettingsDirectory = Path.Combine(sourceRoot, "Settings");
            var sourceDatabaseDirectory = Path.Combine(sourceRoot, "LocalDB");
            Directory.CreateDirectory(sourceSettingsDirectory);
            Directory.CreateDirectory(sourceDatabaseDirectory);
            await File.WriteAllTextAsync(Path.Combine(sourceSettingsDirectory, "app-settings.json"), "{\"ResourceNumber\":\"RES-77\",\"IsSetClientAppInfo\":true}");
            await File.WriteAllTextAsync(Path.Combine(sourceSettingsDirectory, "user-config.json"), "{\"Language\":\"zh-CN\"}");
            await CreateSqliteDatabaseAsync(Path.Combine(sourceDatabaseDirectory, "wear-parts-control.db"), "imported-while-open");

            var packagePath = Path.Combine(workspace, "config.cfg");
            await CreateService(sourceRoot).ExportAsync(packagePath);

            var targetRoot = Path.Combine(workspace, "target");
            var targetSettingsDirectory = Path.Combine(targetRoot, "Settings");
            var targetDatabaseDirectory = Path.Combine(targetRoot, "LocalDB");
            Directory.CreateDirectory(targetSettingsDirectory);
            Directory.CreateDirectory(targetDatabaseDirectory);
            await File.WriteAllTextAsync(Path.Combine(targetSettingsDirectory, "app-settings.json"), "{\"ResourceNumber\":\"\",\"IsSetClientAppInfo\":false}");
            var targetDatabasePath = Path.Combine(targetDatabaseDirectory, "wear-parts-control.db");
            await CreateSqliteDatabaseAsync(targetDatabasePath, "existing-before-import");

            var appSettingsService = new AppSettingsService(new TypeJsonSaveInfoStore(targetSettingsDirectory), targetSettingsDirectory);
            var service = new ConfigurationTransferService(appSettingsService, targetRoot);

            await using (var openedConnection = new SqliteConnection($"Data Source={targetDatabasePath}"))
            {
                await openedConnection.OpenAsync();

                var summary = await service.ImportAsync(packagePath);

                Assert.Equal(packagePath, summary.PackagePath);
                Assert.Equal(3, summary.FileCount);
            }

            Assert.Equal("imported-while-open", await ReadSqliteDatabasePayloadAsync(targetDatabasePath));
            Assert.Equal("{\"Language\":\"zh-CN\"}", await File.ReadAllTextAsync(Path.Combine(targetSettingsDirectory, "user-config.json")));
        }
        finally
        {
            DeleteDirectory(workspace);
        }
    }

    private static ConfigurationTransferService CreateService(string rootDirectory)
    {
        var settingsDirectory = Path.Combine(rootDirectory, "Settings");
        var appSettingsService = CreateAppSettingsService(settingsDirectory);
        return new ConfigurationTransferService(appSettingsService, rootDirectory);
    }

    private static AppSettingsService CreateAppSettingsService(string settingsDirectory)
    {
        return new AppSettingsService(new TypeJsonSaveInfoStore(settingsDirectory), settingsDirectory);
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"WearPartsControl.ConfigTransfer.Tests.{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteDirectory(string path)
    {
        SqliteConnection.ClearAllPools();

        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    private static async Task CreateSqliteDatabaseAsync(string databasePath, string payload)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath) ?? Path.GetTempPath());

        await using var connection = new SqliteConnection($"Data Source={databasePath}");
        await connection.OpenAsync();

        await using (var createCommand = connection.CreateCommand())
        {
            createCommand.CommandText = "CREATE TABLE IF NOT EXISTS import_payload (Value TEXT NOT NULL);";
            await createCommand.ExecuteNonQueryAsync();
        }

        await using (var clearCommand = connection.CreateCommand())
        {
            clearCommand.CommandText = "DELETE FROM import_payload;";
            await clearCommand.ExecuteNonQueryAsync();
        }

        await using (var insertCommand = connection.CreateCommand())
        {
            insertCommand.CommandText = "INSERT INTO import_payload (Value) VALUES ($value);";
            insertCommand.Parameters.AddWithValue("$value", payload);
            await insertCommand.ExecuteNonQueryAsync();
        }
    }

    private static async Task CreateSqliteDatabaseWithClientConfigurationAsync(
        string databasePath,
        string payload,
        TestClientAppConfiguration configuration,
        params TestWearPartDefinition[] wearPartDefinitions)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath) ?? Path.GetTempPath());

        await using var connection = new SqliteConnection($"Data Source={databasePath}");
        await connection.OpenAsync();

        await using (var createPayloadCommand = connection.CreateCommand())
        {
            createPayloadCommand.CommandText = "CREATE TABLE IF NOT EXISTS import_payload (Value TEXT NOT NULL);";
            await createPayloadCommand.ExecuteNonQueryAsync();
        }

        await using (var createConfigurationsCommand = connection.CreateCommand())
        {
            createConfigurationsCommand.CommandText = """
CREATE TABLE IF NOT EXISTS basic_configurations (
    Id TEXT NOT NULL PRIMARY KEY,
    CreatedAt TEXT NOT NULL,
    UpdatedAt TEXT NOT NULL,
    CreatedBy TEXT NOT NULL,
    UpdatedBy TEXT NOT NULL,
    SiteCode TEXT NOT NULL,
    FactoryCode TEXT NOT NULL,
    AreaCode TEXT NOT NULL,
    ProcedureCode TEXT NOT NULL,
    EquipmentCode TEXT NOT NULL,
    ResourceNumber TEXT NOT NULL,
    PlcProtocolType TEXT NOT NULL,
    PlcIpAddress TEXT NOT NULL,
    PlcPort INTEGER NOT NULL,
    ShutdownPointAddress TEXT NOT NULL,
    EnableCutterMesValidation INTEGER NOT NULL,
    CutterMesWsdl TEXT NOT NULL,
    CutterMesUser TEXT NOT NULL,
    CutterMesPassword TEXT NOT NULL,
    CutterMesSite TEXT NOT NULL,
    SiemensRack INTEGER NOT NULL,
    SiemensSlot INTEGER NOT NULL,
    IsStringReverse INTEGER NOT NULL,
    HostIpAddress TEXT NOT NULL DEFAULT ''
);
""";
            await createConfigurationsCommand.ExecuteNonQueryAsync();
        }

        await using (var createWearPartDefinitionsCommand = connection.CreateCommand())
        {
            createWearPartDefinitionsCommand.CommandText = """
CREATE TABLE IF NOT EXISTS wear_part_definitions (
    Id TEXT NOT NULL PRIMARY KEY,
    ClientAppConfigurationId TEXT NOT NULL,
    ResourceNumber TEXT NOT NULL,
    PartCode TEXT NOT NULL,
    FOREIGN KEY (ClientAppConfigurationId) REFERENCES basic_configurations(Id) ON DELETE RESTRICT
);
""";
            await createWearPartDefinitionsCommand.ExecuteNonQueryAsync();
        }

        await using (var clearWearPartDefinitionsCommand = connection.CreateCommand())
        {
            clearWearPartDefinitionsCommand.CommandText = "DELETE FROM wear_part_definitions;";
            await clearWearPartDefinitionsCommand.ExecuteNonQueryAsync();
        }

        await using (var clearConfigurationsCommand = connection.CreateCommand())
        {
            clearConfigurationsCommand.CommandText = "DELETE FROM basic_configurations;";
            await clearConfigurationsCommand.ExecuteNonQueryAsync();
        }

        await using (var clearPayloadCommand = connection.CreateCommand())
        {
            clearPayloadCommand.CommandText = "DELETE FROM import_payload;";
            await clearPayloadCommand.ExecuteNonQueryAsync();
        }

        await using (var insertPayloadCommand = connection.CreateCommand())
        {
            insertPayloadCommand.CommandText = "INSERT INTO import_payload (Value) VALUES ($value);";
            insertPayloadCommand.Parameters.AddWithValue("$value", payload);
            await insertPayloadCommand.ExecuteNonQueryAsync();
        }

        await InsertClientAppConfigurationAsync(connection, configuration);

        foreach (var wearPartDefinition in wearPartDefinitions)
        {
            await InsertWearPartDefinitionAsync(connection, wearPartDefinition);
        }
    }

    private static async Task InsertClientAppConfigurationAsync(SqliteConnection connection, TestClientAppConfiguration configuration)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
INSERT INTO basic_configurations (
    Id, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy, SiteCode, FactoryCode, AreaCode, ProcedureCode,
    EquipmentCode, ResourceNumber, PlcProtocolType, PlcIpAddress, PlcPort, ShutdownPointAddress,
    EnableCutterMesValidation, CutterMesWsdl, CutterMesUser, CutterMesPassword, CutterMesSite,
    SiemensRack, SiemensSlot, IsStringReverse, HostIpAddress)
VALUES (
    $id, $createdAt, $updatedAt, $createdBy, $updatedBy, $siteCode, $factoryCode, $areaCode, $procedureCode,
    $equipmentCode, $resourceNumber, $plcProtocolType, $plcIpAddress, $plcPort, $shutdownPointAddress,
    $enableCutterMesValidation, $cutterMesWsdl, $cutterMesUser, $cutterMesPassword, $cutterMesSite,
    $siemensRack, $siemensSlot, $isStringReverse, $hostIpAddress);
""";
        command.Parameters.AddWithValue("$id", configuration.Id);
        command.Parameters.AddWithValue("$createdAt", "2026-05-13T00:00:00+08:00");
        command.Parameters.AddWithValue("$updatedAt", "2026-05-13T00:00:00+08:00");
        command.Parameters.AddWithValue("$createdBy", "test");
        command.Parameters.AddWithValue("$updatedBy", "test");
        command.Parameters.AddWithValue("$siteCode", configuration.SiteCode);
        command.Parameters.AddWithValue("$factoryCode", configuration.FactoryCode);
        command.Parameters.AddWithValue("$areaCode", configuration.AreaCode);
        command.Parameters.AddWithValue("$procedureCode", configuration.ProcedureCode);
        command.Parameters.AddWithValue("$equipmentCode", configuration.EquipmentCode);
        command.Parameters.AddWithValue("$resourceNumber", configuration.ResourceNumber);
        command.Parameters.AddWithValue("$plcProtocolType", configuration.PlcProtocolType);
        command.Parameters.AddWithValue("$plcIpAddress", configuration.PlcIpAddress);
        command.Parameters.AddWithValue("$plcPort", configuration.PlcPort);
        command.Parameters.AddWithValue("$shutdownPointAddress", configuration.ShutdownPointAddress);
        command.Parameters.AddWithValue("$enableCutterMesValidation", configuration.EnableCutterMesValidation ? 1 : 0);
        command.Parameters.AddWithValue("$cutterMesWsdl", configuration.CutterMesWsdl);
        command.Parameters.AddWithValue("$cutterMesUser", configuration.CutterMesUser);
        command.Parameters.AddWithValue("$cutterMesPassword", configuration.CutterMesPassword);
        command.Parameters.AddWithValue("$cutterMesSite", configuration.CutterMesSite);
        command.Parameters.AddWithValue("$siemensRack", configuration.SiemensRack);
        command.Parameters.AddWithValue("$siemensSlot", configuration.SiemensSlot);
        command.Parameters.AddWithValue("$isStringReverse", configuration.IsStringReverse ? 1 : 0);
        command.Parameters.AddWithValue("$hostIpAddress", configuration.HostIpAddress);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task InsertWearPartDefinitionAsync(SqliteConnection connection, TestWearPartDefinition wearPartDefinition)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
INSERT INTO wear_part_definitions (Id, ClientAppConfigurationId, ResourceNumber, PartCode)
VALUES ($id, $clientAppConfigurationId, $resourceNumber, $partCode);
""";
        command.Parameters.AddWithValue("$id", wearPartDefinition.Id);
        command.Parameters.AddWithValue("$clientAppConfigurationId", wearPartDefinition.ClientAppConfigurationId);
        command.Parameters.AddWithValue("$resourceNumber", wearPartDefinition.ResourceNumber);
        command.Parameters.AddWithValue("$partCode", wearPartDefinition.PartCode);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<TestClientAppConfiguration?> ReadClientAppConfigurationAsync(string databasePath, string resourceNumber)
    {
        await using var connection = new SqliteConnection($"Data Source={databasePath}");
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = """
SELECT Id, SiteCode, FactoryCode, AreaCode, ProcedureCode, EquipmentCode, ResourceNumber, PlcProtocolType,
       PlcIpAddress, PlcPort, ShutdownPointAddress, EnableCutterMesValidation, CutterMesWsdl, CutterMesUser,
       CutterMesPassword, CutterMesSite, SiemensRack, SiemensSlot, IsStringReverse, HostIpAddress
FROM basic_configurations
WHERE ResourceNumber = $resourceNumber
LIMIT 1;
""";
        command.Parameters.AddWithValue("$resourceNumber", resourceNumber);

        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return null;
        }

        return new TestClientAppConfiguration(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.GetString(5),
            reader.GetString(6),
            reader.GetString(7),
            reader.GetString(8),
            reader.GetInt32(9),
            reader.GetString(10),
            reader.GetInt64(11) != 0,
            reader.GetString(12),
            reader.GetString(13),
            reader.GetString(14),
            reader.GetString(15),
            reader.GetInt32(16),
            reader.GetInt32(17),
            reader.GetInt64(18) != 0,
            reader.GetString(19));
    }

    private static async Task<TestWearPartDefinition?> ReadWearPartDefinitionAsync(string databasePath, string partCode)
    {
        await using var connection = new SqliteConnection($"Data Source={databasePath}");
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, ClientAppConfigurationId, ResourceNumber, PartCode FROM wear_part_definitions WHERE PartCode = $partCode LIMIT 1;";
        command.Parameters.AddWithValue("$partCode", partCode);

        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return null;
        }

        return new TestWearPartDefinition(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3));
    }

    private static async Task<string?> ReadSqliteDatabasePayloadAsync(string databasePath)
    {
        await using var connection = new SqliteConnection($"Data Source={databasePath}");
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Value FROM import_payload LIMIT 1;";
        return (string?)await command.ExecuteScalarAsync();
    }

    private sealed record TestClientAppConfiguration(
        string Id,
        string SiteCode,
        string FactoryCode,
        string AreaCode,
        string ProcedureCode,
        string EquipmentCode,
        string ResourceNumber,
        string PlcProtocolType,
        string PlcIpAddress,
        int PlcPort,
        string ShutdownPointAddress,
        bool EnableCutterMesValidation,
        string CutterMesWsdl,
        string CutterMesUser,
        string CutterMesPassword,
        string CutterMesSite,
        int SiemensRack,
        int SiemensSlot,
        bool IsStringReverse,
        string HostIpAddress);

    private sealed record TestWearPartDefinition(
        string Id,
        string ClientAppConfigurationId,
        string ResourceNumber,
        string PartCode);
}