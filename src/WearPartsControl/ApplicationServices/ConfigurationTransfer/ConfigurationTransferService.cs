using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text.Json;
using System.Data.Common;
using AppSettingsModel = WearPartsControl.ApplicationServices.AppSettings.AppSettings;
using Microsoft.Data.Sqlite;
using WearPartsControl.ApplicationServices.AppSettings;
using WearPartsControl.ApplicationServices.Localization;
using WearPartsControl.Exceptions;

namespace WearPartsControl.ApplicationServices.ConfigurationTransfer;

public sealed class ConfigurationTransferService : IConfigurationTransferService
{
    private const int CurrentFormatVersion = 1;
    private const string ManifestEntryName = "configuration-package.json";
    private const string SettingsRootName = "Settings";
    private const string AppSettingsFileName = "app-settings.json";
    private const string LocalDatabaseRootName = "LocalDB";
    private const string LocalDatabaseFileName = "wear-parts-control.db";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = false,
        WriteIndented = true
    };

    private static readonly string[] IncludedRootNames = [SettingsRootName, LocalDatabaseRootName];
    private static readonly string[] ImportRootNames = [LocalDatabaseRootName, SettingsRootName];
    private static readonly HashSet<string> IncludedRootNameSet = new(IncludedRootNames, StringComparer.OrdinalIgnoreCase);

    private readonly IAppSettingsService _appSettingsService;
    private readonly string _rootDirectory;

    public ConfigurationTransferService(IAppSettingsService appSettingsService, string? rootDirectory = null)
    {
        _appSettingsService = appSettingsService;
        _rootDirectory = Path.GetFullPath(rootDirectory ?? PortableDataPaths.RootDirectory);
        Directory.CreateDirectory(_rootDirectory);
    }

    public async Task<ConfigurationTransferSummary> ExportAsync(string packagePath, CancellationToken cancellationToken = default)
    {
        packagePath = NormalizePackagePath(packagePath);
        Directory.CreateDirectory(Path.GetDirectoryName(packagePath) ?? Environment.CurrentDirectory);

        var tempPath = packagePath + ".tmp";
        if (File.Exists(tempPath))
        {
            File.Delete(tempPath);
        }

        var fileCount = 0;
        await using (var output = new FileStream(tempPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 64 * 1024, FileOptions.WriteThrough))
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: false))
        {
            var manifest = new ConfigurationPackageManifest(
                CurrentFormatVersion,
                "WearPartsControl",
                ResolveProductVersion(),
                DateTimeOffset.UtcNow,
                IncludedRootNames);

            var manifestEntry = archive.CreateEntry(ManifestEntryName, CompressionLevel.Optimal);
            await using (var manifestStream = manifestEntry.Open())
            {
                await JsonSerializer.SerializeAsync(manifestStream, manifest, SerializerOptions, cancellationToken).ConfigureAwait(false);
            }

            foreach (var rootName in IncludedRootNames)
            {
                var sourceRoot = Path.Combine(_rootDirectory, rootName);
                if (!Directory.Exists(sourceRoot))
                {
                    continue;
                }

                foreach (var filePath in Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (ShouldSkipFile(filePath, packagePath, tempPath))
                    {
                        continue;
                    }

                    var relativePath = Path.GetRelativePath(sourceRoot, filePath).Replace(Path.DirectorySeparatorChar, '/');
                    var entryName = rootName + "/" + relativePath.Replace(Path.AltDirectorySeparatorChar, '/');
                    var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);

                    await using var entryStream = entry.Open();
                    await using var input = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 64 * 1024, FileOptions.SequentialScan);
                    await input.CopyToAsync(entryStream, cancellationToken).ConfigureAwait(false);
                    fileCount++;
                }
            }
        }

        File.Move(tempPath, packagePath, overwrite: true);
        return new ConfigurationTransferSummary(packagePath, fileCount);
    }

    public async Task<ConfigurationTransferSummary> ImportAsync(string packagePath, CancellationToken cancellationToken = default)
    {
        packagePath = NormalizePackagePath(packagePath);
        if (!File.Exists(packagePath))
        {
            throw new UserFriendlyException(LocalizedText.Format("Services.ConfigurationTransfer.PackageNotFound", packagePath));
        }

        var currentSettings = await _appSettingsService.GetAsync(cancellationToken).ConfigureAwait(false);
        var preservedClientAppConfiguration = default(ClientAppConfigurationSnapshot);
        if (HasConfiguredClientAppInfo(currentSettings))
        {
            preservedClientAppConfiguration = await ReadClientAppConfigurationSnapshotAsync(
                Path.Combine(_rootDirectory, LocalDatabaseRootName, LocalDatabaseFileName),
                currentSettings.ResourceNumber,
                cancellationToken).ConfigureAwait(false)
                ?? throw new UserFriendlyException(LocalizedText.Format("Services.ConfigurationTransfer.CurrentClientConfigurationMissing", currentSettings.ResourceNumber));
        }

        var stagingDirectory = Path.Combine(Path.GetTempPath(), "WearPartsControl-ConfigImport-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stagingDirectory);

        try
        {
            var fileCount = await ExtractToStagingDirectoryAsync(packagePath, stagingDirectory, cancellationToken).ConfigureAwait(false);
            var importContext = new ImportContext(
                currentSettings,
                await ReadImportedAppSettingsAsync(stagingDirectory, cancellationToken).ConfigureAwait(false),
                preservedClientAppConfiguration);

            foreach (var rootName in ImportRootNames)
            {
                var sourceRoot = Path.Combine(stagingDirectory, rootName);
                if (!Directory.Exists(sourceRoot))
                {
                    continue;
                }

                var destinationRoot = Path.Combine(_rootDirectory, rootName);
                await ImportRootAsync(rootName, sourceRoot, destinationRoot, importContext, cancellationToken).ConfigureAwait(false);
            }

            var importedSettings = await _appSettingsService.GetAsync(cancellationToken).ConfigureAwait(false);
            await _appSettingsService.SaveAsync(MergeImportedSettings(importContext, importedSettings), cancellationToken).ConfigureAwait(false);

            return new ConfigurationTransferSummary(packagePath, fileCount);
        }
        finally
        {
            if (Directory.Exists(stagingDirectory))
            {
                Directory.Delete(stagingDirectory, recursive: true);
            }
        }
    }

    private static string NormalizePackagePath(string packagePath)
    {
        if (string.IsNullOrWhiteSpace(packagePath))
        {
            throw new UserFriendlyException(LocalizedText.Get("Services.ConfigurationTransfer.PackagePathRequired"));
        }

        packagePath = Path.GetFullPath(packagePath.Trim());
        if (!string.Equals(Path.GetExtension(packagePath), ".cfg", StringComparison.OrdinalIgnoreCase))
        {
            throw new UserFriendlyException(LocalizedText.Get("Services.ConfigurationTransfer.PackageExtensionInvalid"));
        }

        return packagePath;
    }

    private static bool ShouldSkipFile(string filePath, string packagePath, string tempPath)
    {
        var fullPath = Path.GetFullPath(filePath);
        if (string.Equals(fullPath, packagePath, StringComparison.OrdinalIgnoreCase)
            || string.Equals(fullPath, tempPath, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var fileName = Path.GetFileName(filePath);
        return fileName.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase)
            || fileName.EndsWith(".log", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<int> ExtractToStagingDirectoryAsync(string packagePath, string stagingDirectory, CancellationToken cancellationToken)
    {
        var stagingRoot = EnsureTrailingDirectorySeparator(Path.GetFullPath(stagingDirectory));
        using var archive = ZipFile.OpenRead(packagePath);
        var manifestEntry = archive.GetEntry(ManifestEntryName)
            ?? throw new UserFriendlyException(LocalizedText.Get("Services.ConfigurationTransfer.ManifestMissing"));

        await using (var manifestStream = manifestEntry.Open())
        {
            var manifest = await JsonSerializer.DeserializeAsync<ConfigurationPackageManifest>(manifestStream, SerializerOptions, cancellationToken).ConfigureAwait(false)
                ?? throw new UserFriendlyException(LocalizedText.Get("Services.ConfigurationTransfer.ManifestInvalid"));

            if (manifest.FormatVersion != CurrentFormatVersion)
            {
                throw new UserFriendlyException(LocalizedText.Format("Services.ConfigurationTransfer.UnsupportedFormatVersion", manifest.FormatVersion));
            }

            if (!string.Equals(manifest.ProductName, "WearPartsControl", StringComparison.Ordinal))
            {
                throw new UserFriendlyException(LocalizedText.Get("Services.ConfigurationTransfer.ProductMismatch"));
            }
        }

        var fileCount = 0;
        foreach (var entry in archive.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.Equals(entry.FullName, ManifestEntryName, StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(entry.Name))
            {
                continue;
            }

            var normalizedEntryName = NormalizeEntryName(entry.FullName);
            var destinationPath = Path.GetFullPath(Path.Combine(stagingDirectory, normalizedEntryName));
            if (!destinationPath.StartsWith(stagingRoot, StringComparison.OrdinalIgnoreCase))
            {
                throw new UserFriendlyException(LocalizedText.Get("Services.ConfigurationTransfer.PackagePathInvalid"));
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath) ?? stagingDirectory);
            entry.ExtractToFile(destinationPath, overwrite: true);
            fileCount++;
        }

        return fileCount;
    }

    private static string NormalizeEntryName(string entryName)
    {
        var normalized = entryName.Replace('\\', '/').Trim('/');
        if (string.IsNullOrWhiteSpace(normalized)
            || normalized.Contains("..", StringComparison.Ordinal)
            || Path.IsPathRooted(normalized))
        {
            throw new UserFriendlyException(LocalizedText.Get("Services.ConfigurationTransfer.PackagePathInvalid"));
        }

        var firstSeparatorIndex = normalized.IndexOf('/', StringComparison.Ordinal);
        if (firstSeparatorIndex <= 0 || firstSeparatorIndex == normalized.Length - 1)
        {
            throw new UserFriendlyException(LocalizedText.Get("Services.ConfigurationTransfer.PackagePathInvalid"));
        }

        var rootName = firstSeparatorIndex < 0 ? normalized : normalized[..firstSeparatorIndex];
        var canonicalRootName = IncludedRootNames.FirstOrDefault(includedRootName => string.Equals(includedRootName, rootName, StringComparison.OrdinalIgnoreCase));
        if (canonicalRootName is null)
        {
            throw new UserFriendlyException(LocalizedText.Format("Services.ConfigurationTransfer.PackageRootInvalid", rootName));
        }

        var relativePath = normalized[(firstSeparatorIndex + 1)..];
        return Path.Combine(canonicalRootName, relativePath.Replace('/', Path.DirectorySeparatorChar));
    }

    private static string EnsureTrailingDirectorySeparator(string path)
    {
        return path.EndsWith(Path.DirectorySeparatorChar)
            ? path
            : path + Path.DirectorySeparatorChar;
    }

    private static bool HasConfiguredClientAppInfo(AppSettingsModel settings)
    {
        return settings.IsSetClientAppInfo && !string.IsNullOrWhiteSpace(settings.ResourceNumber);
    }

    private static AppSettingsModel MergeImportedSettings(ImportContext importContext, AppSettingsModel importedSettings)
    {
        if (!importContext.PreserveCurrentClientAppInfo)
        {
            return importedSettings;
        }

        importedSettings.ResourceNumber = importContext.CurrentSettings.ResourceNumber;
        importedSettings.IsSetClientAppInfo = importContext.CurrentSettings.IsSetClientAppInfo;
        return importedSettings;
    }

    private static async Task<AppSettingsModel> ReadImportedAppSettingsAsync(string stagingDirectory, CancellationToken cancellationToken)
    {
        var appSettingsPath = Path.Combine(stagingDirectory, SettingsRootName, AppSettingsFileName);
        if (!File.Exists(appSettingsPath))
        {
            return new AppSettingsModel();
        }

        await using var stream = new FileStream(appSettingsPath, FileMode.Open, FileAccess.Read, FileShare.Read, 16 * 1024, FileOptions.SequentialScan);
        return await JsonSerializer.DeserializeAsync<AppSettingsModel>(stream, SerializerOptions, cancellationToken).ConfigureAwait(false)
            ?? new AppSettingsModel();
    }

    private static async Task<ClientAppConfigurationSnapshot?> ReadClientAppConfigurationSnapshotAsync(string databasePath, string resourceNumber, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(resourceNumber) || !File.Exists(databasePath))
        {
            return null;
        }

        await using var connection = new SqliteConnection(BuildSqliteConnectionString(databasePath, SqliteOpenMode.ReadOnly));
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText = """
SELECT Id, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy, SiteCode, FactoryCode, AreaCode, ProcedureCode,
       EquipmentCode, ResourceNumber, PlcProtocolType, PlcIpAddress, PlcPort, ShutdownPointAddress,
       EnableCutterMesValidation, CutterMesWsdl, CutterMesUser, CutterMesPassword, CutterMesSite,
       SiemensRack, SiemensSlot, IsStringReverse, HostIpAddress
FROM basic_configurations
WHERE ResourceNumber = $resourceNumber
LIMIT 1;
""";
        command.Parameters.AddWithValue("$resourceNumber", resourceNumber.Trim());

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return new ClientAppConfigurationSnapshot(
            GetString(reader, 0),
            GetString(reader, 1),
            GetString(reader, 2),
            GetString(reader, 3),
            GetString(reader, 4),
            GetString(reader, 5),
            GetString(reader, 6),
            GetString(reader, 7),
            GetString(reader, 8),
            GetString(reader, 9),
            GetString(reader, 10),
            GetString(reader, 11),
            GetString(reader, 12),
            reader.GetInt32(13),
            GetString(reader, 14),
            reader.GetInt64(15) != 0,
            GetString(reader, 16),
            GetString(reader, 17),
            GetString(reader, 18),
            GetString(reader, 19),
            reader.GetInt32(20),
            reader.GetInt32(21),
            reader.GetInt64(22) != 0,
            GetString(reader, 23));
    }

    private static async Task ImportRootAsync(string rootName, string sourceRoot, string destinationRoot, ImportContext importContext, CancellationToken cancellationToken)
    {
        if (string.Equals(rootName, LocalDatabaseRootName, StringComparison.Ordinal))
        {
            await ImportLocalDatabaseAsync(sourceRoot, destinationRoot, importContext, cancellationToken).ConfigureAwait(false);
            return;
        }

        ReplaceDirectory(sourceRoot, destinationRoot);
    }

    private static async Task ImportLocalDatabaseAsync(string sourceRoot, string destinationRoot, ImportContext importContext, CancellationToken cancellationToken)
    {
        var sourceDatabasePath = Path.Combine(sourceRoot, LocalDatabaseFileName);
        if (!File.Exists(sourceDatabasePath))
        {
            throw new UserFriendlyException(LocalizedText.Get("Services.ConfigurationTransfer.DatabaseFileMissing"));
        }

        Directory.CreateDirectory(destinationRoot);
        var destinationDatabasePath = Path.Combine(destinationRoot, LocalDatabaseFileName);

        SqliteConnection.ClearAllPools();

        try
        {
            await using var sourceConnection = new SqliteConnection(BuildSqliteConnectionString(sourceDatabasePath, SqliteOpenMode.ReadOnly));
            await using var destinationConnection = new SqliteConnection(BuildSqliteConnectionString(destinationDatabasePath, SqliteOpenMode.ReadWriteCreate));

            await sourceConnection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await destinationConnection.OpenAsync(cancellationToken).ConfigureAwait(false);
            sourceConnection.BackupDatabase(destinationConnection);

            if (importContext.PreserveCurrentClientAppInfo)
            {
                await PreserveCurrentClientAppConfigurationAsync(
                    destinationConnection,
                    importContext.PreservedClientAppConfiguration!,
                    importContext.ImportedSettings.ResourceNumber,
                    cancellationToken).ConfigureAwait(false);
            }
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode is 5 or 6)
        {
            throw new UserFriendlyException(LocalizedText.Get("Services.ConfigurationTransfer.DatabaseBusy"));
        }
        catch (SqliteException ex)
        {
            throw new UserFriendlyException(LocalizedText.Format("Services.ConfigurationTransfer.DatabaseImportFailed", ex.Message));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
        }

        ReplaceDirectoryContents(sourceRoot, destinationRoot, IsLocalDatabaseFileOrSidecar, static sourceFilePath => !IsLocalDatabaseFileOrSidecar(sourceFilePath));
    }

    private static async Task PreserveCurrentClientAppConfigurationAsync(
        SqliteConnection destinationConnection,
        ClientAppConfigurationSnapshot preservedClientAppConfiguration,
        string importedResourceNumber,
        CancellationToken cancellationToken)
    {
        await ExecuteNonQueryAsync(destinationConnection, "PRAGMA foreign_keys = ON;", cancellationToken).ConfigureAwait(false);

        var normalizedImportedResourceNumber = NormalizeOptional(importedResourceNumber);
        var sourceConfigurationId = string.IsNullOrWhiteSpace(normalizedImportedResourceNumber)
            ? null
            : await FindClientAppConfigurationIdByResourceNumberAsync(destinationConnection, normalizedImportedResourceNumber, cancellationToken).ConfigureAwait(false);
        var currentResourceConfigurationId = await FindClientAppConfigurationIdByResourceNumberAsync(destinationConnection, preservedClientAppConfiguration.ResourceNumber, cancellationToken).ConfigureAwait(false);

        await using var transaction = await destinationConnection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!string.IsNullOrWhiteSpace(sourceConfigurationId))
            {
                if (!string.IsNullOrWhiteSpace(currentResourceConfigurationId)
                    && !string.Equals(currentResourceConfigurationId, sourceConfigurationId, StringComparison.OrdinalIgnoreCase))
                {
                    await DeleteClientAppConfigurationAsync(destinationConnection, currentResourceConfigurationId, transaction, cancellationToken).ConfigureAwait(false);
                }

                await UpdateClientAppConfigurationAsync(destinationConnection, sourceConfigurationId, preservedClientAppConfiguration, transaction, cancellationToken).ConfigureAwait(false);
                await UpdateWearPartDefinitionResourceNumberAsync(destinationConnection, sourceConfigurationId, preservedClientAppConfiguration.ResourceNumber, transaction, cancellationToken).ConfigureAwait(false);
            }
            else if (!string.IsNullOrWhiteSpace(currentResourceConfigurationId))
            {
                await UpdateClientAppConfigurationAsync(destinationConnection, currentResourceConfigurationId, preservedClientAppConfiguration, transaction, cancellationToken).ConfigureAwait(false);
                await UpdateWearPartDefinitionResourceNumberAsync(destinationConnection, currentResourceConfigurationId, preservedClientAppConfiguration.ResourceNumber, transaction, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await InsertClientAppConfigurationAsync(destinationConnection, preservedClientAppConfiguration, transaction, cancellationToken).ConfigureAwait(false);
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    private static async Task<string?> FindClientAppConfigurationIdByResourceNumberAsync(SqliteConnection connection, string resourceNumber, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id FROM basic_configurations WHERE ResourceNumber = $resourceNumber LIMIT 1;";
        command.Parameters.AddWithValue("$resourceNumber", resourceNumber);
        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return result as string;
    }

    private static async Task DeleteClientAppConfigurationAsync(SqliteConnection connection, string configurationId, DbTransaction transaction, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = (SqliteTransaction)transaction;
        command.CommandText = "DELETE FROM basic_configurations WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", configurationId);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task UpdateClientAppConfigurationAsync(
        SqliteConnection connection,
        string configurationId,
        ClientAppConfigurationSnapshot snapshot,
        DbTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = (SqliteTransaction)transaction;
        command.CommandText = """
UPDATE basic_configurations
SET CreatedAt = $createdAt,
    UpdatedAt = $updatedAt,
    CreatedBy = $createdBy,
    UpdatedBy = $updatedBy,
    SiteCode = $siteCode,
    FactoryCode = $factoryCode,
    AreaCode = $areaCode,
    ProcedureCode = $procedureCode,
    EquipmentCode = $equipmentCode,
    ResourceNumber = $resourceNumber,
    PlcProtocolType = $plcProtocolType,
    PlcIpAddress = $plcIpAddress,
    PlcPort = $plcPort,
    ShutdownPointAddress = $shutdownPointAddress,
    EnableCutterMesValidation = $enableCutterMesValidation,
    CutterMesWsdl = $cutterMesWsdl,
    CutterMesUser = $cutterMesUser,
    CutterMesPassword = $cutterMesPassword,
    CutterMesSite = $cutterMesSite,
    SiemensRack = $siemensRack,
    SiemensSlot = $siemensSlot,
    IsStringReverse = $isStringReverse,
    HostIpAddress = $hostIpAddress
WHERE Id = $configurationId;
""";
        AddClientAppConfigurationParameters(command, snapshot);
        command.Parameters.AddWithValue("$configurationId", configurationId);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task InsertClientAppConfigurationAsync(
        SqliteConnection connection,
        ClientAppConfigurationSnapshot snapshot,
        DbTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = (SqliteTransaction)transaction;
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
        AddClientAppConfigurationParameters(command, snapshot);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static void AddClientAppConfigurationParameters(SqliteCommand command, ClientAppConfigurationSnapshot snapshot)
    {
        command.Parameters.AddWithValue("$id", snapshot.Id);
        command.Parameters.AddWithValue("$createdAt", snapshot.CreatedAt);
        command.Parameters.AddWithValue("$updatedAt", snapshot.UpdatedAt);
        command.Parameters.AddWithValue("$createdBy", snapshot.CreatedBy);
        command.Parameters.AddWithValue("$updatedBy", snapshot.UpdatedBy);
        command.Parameters.AddWithValue("$siteCode", snapshot.SiteCode);
        command.Parameters.AddWithValue("$factoryCode", snapshot.FactoryCode);
        command.Parameters.AddWithValue("$areaCode", snapshot.AreaCode);
        command.Parameters.AddWithValue("$procedureCode", snapshot.ProcedureCode);
        command.Parameters.AddWithValue("$equipmentCode", snapshot.EquipmentCode);
        command.Parameters.AddWithValue("$resourceNumber", snapshot.ResourceNumber);
        command.Parameters.AddWithValue("$plcProtocolType", snapshot.PlcProtocolType);
        command.Parameters.AddWithValue("$plcIpAddress", snapshot.PlcIpAddress);
        command.Parameters.AddWithValue("$plcPort", snapshot.PlcPort);
        command.Parameters.AddWithValue("$shutdownPointAddress", snapshot.ShutdownPointAddress);
        command.Parameters.AddWithValue("$enableCutterMesValidation", snapshot.EnableCutterMesValidation ? 1 : 0);
        command.Parameters.AddWithValue("$cutterMesWsdl", snapshot.CutterMesWsdl);
        command.Parameters.AddWithValue("$cutterMesUser", snapshot.CutterMesUser);
        command.Parameters.AddWithValue("$cutterMesPassword", snapshot.CutterMesPassword);
        command.Parameters.AddWithValue("$cutterMesSite", snapshot.CutterMesSite);
        command.Parameters.AddWithValue("$siemensRack", snapshot.SiemensRack);
        command.Parameters.AddWithValue("$siemensSlot", snapshot.SiemensSlot);
        command.Parameters.AddWithValue("$isStringReverse", snapshot.IsStringReverse ? 1 : 0);
        command.Parameters.AddWithValue("$hostIpAddress", snapshot.HostIpAddress);
    }

    private static async Task UpdateWearPartDefinitionResourceNumberAsync(
        SqliteConnection connection,
        string configurationId,
        string resourceNumber,
        DbTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = (SqliteTransaction)transaction;
        command.CommandText = "UPDATE wear_part_definitions SET ResourceNumber = $resourceNumber WHERE ClientAppConfigurationId = $configurationId;";
        command.Parameters.AddWithValue("$resourceNumber", resourceNumber);
        command.Parameters.AddWithValue("$configurationId", configurationId);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task ExecuteNonQueryAsync(SqliteConnection connection, string commandText, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = commandText;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static string GetString(SqliteDataReader reader, int ordinal)
    {
        return reader.IsDBNull(ordinal) ? string.Empty : reader.GetString(ordinal);
    }

    private static string NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private static string BuildSqliteConnectionString(string databasePath, SqliteOpenMode openMode)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = openMode,
            Pooling = false
        };

        return builder.ToString();
    }

    private static bool IsLocalDatabaseFileOrSidecar(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        return string.Equals(fileName, LocalDatabaseFileName, StringComparison.OrdinalIgnoreCase)
            || string.Equals(fileName, LocalDatabaseFileName + "-wal", StringComparison.OrdinalIgnoreCase)
            || string.Equals(fileName, LocalDatabaseFileName + "-shm", StringComparison.OrdinalIgnoreCase)
            || string.Equals(fileName, LocalDatabaseFileName + "-journal", StringComparison.OrdinalIgnoreCase);
    }

    private static void ReplaceDirectory(string sourceRoot, string destinationRoot)
    {
        ReplaceDirectoryContents(sourceRoot, destinationRoot, static _ => false, static _ => true);
    }

    private static void ReplaceDirectoryContents(
        string sourceRoot,
        string destinationRoot,
        Func<string, bool> preserveDestinationFile,
        Func<string, bool> includeSourceFile)
    {
        Directory.CreateDirectory(destinationRoot);

        foreach (var filePath in Directory.EnumerateFiles(destinationRoot, "*", SearchOption.AllDirectories))
        {
            if (preserveDestinationFile(filePath))
            {
                continue;
            }

            File.Delete(filePath);
        }

        DeleteEmptyDirectories(destinationRoot);

        foreach (var sourceFilePath in Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories))
        {
            if (!includeSourceFile(sourceFilePath))
            {
                continue;
            }

            var relativePath = Path.GetRelativePath(sourceRoot, sourceFilePath);
            var destinationPath = Path.Combine(destinationRoot, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath) ?? destinationRoot);
            File.Copy(sourceFilePath, destinationPath, overwrite: true);
        }
    }

    private static void DeleteEmptyDirectories(string rootPath)
    {
        foreach (var directoryPath in Directory.EnumerateDirectories(rootPath, "*", SearchOption.AllDirectories)
                     .OrderByDescending(static path => path.Length))
        {
            if (!Directory.EnumerateFileSystemEntries(directoryPath).Any())
            {
                Directory.Delete(directoryPath);
            }
        }
    }

    private static string ResolveProductVersion()
    {
        return typeof(ConfigurationTransferService).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? typeof(ConfigurationTransferService).Assembly.GetName().Version?.ToString()
            ?? string.Empty;
    }

    private sealed record ImportContext(
        AppSettingsModel CurrentSettings,
        AppSettingsModel ImportedSettings,
        ClientAppConfigurationSnapshot? PreservedClientAppConfiguration)
    {
        public bool PreserveCurrentClientAppInfo => PreservedClientAppConfiguration is not null;
    }

    private sealed record ClientAppConfigurationSnapshot(
        string Id,
        string CreatedAt,
        string UpdatedAt,
        string CreatedBy,
        string UpdatedBy,
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

    private sealed record ConfigurationPackageManifest(
        int FormatVersion,
        string ProductName,
        string ProductVersion,
        DateTimeOffset ExportedAt,
        IReadOnlyList<string> IncludedRoots);
}