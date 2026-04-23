using System.IO;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using WearPartsControl.Domain.Entities;
using WearPartsControl.Exceptions;
using WearPartsControl.Infrastructure.EntityFrameworkCore;

namespace WearPartsControl.ApplicationServices.LegacyImport;

public sealed class LegacyDatabaseImportService : ILegacyDatabaseImportService
{
    private static readonly IReadOnlyDictionary<string, string> LifetimeTypeAliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["Meter"] = "记米",
        ["Count"] = "计次",
        ["Time"] = "计时",
        ["记米"] = "记米",
        ["计次"] = "计次",
        ["计时"] = "计时"
    };

    private const string ShutdownSeverity = "Shutdown";

    private readonly IDbContextFactory<WearPartsControlDbContext> _dbContextFactory;

    public LegacyDatabaseImportService(IDbContextFactory<WearPartsControlDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<LegacyDatabaseImportResult> ImportAsync(string legacyDatabasePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(legacyDatabasePath))
        {
            throw new UserFriendlyException("旧版 SQLite 数据库文件路径不能为空。");
        }

        var fullPath = Path.GetFullPath(legacyDatabasePath);
        if (!File.Exists(fullPath))
        {
            throw new UserFriendlyException($"未找到旧版 SQLite 数据库文件：{fullPath}");
        }

        var result = new LegacyDatabaseImportResult
        {
            LegacyDatabasePath = fullPath
        };

        await using var legacyConnection = new SqliteConnection($"Data Source={fullPath}");
        await legacyConnection.OpenAsync(cancellationToken).ConfigureAwait(false);

        var legacyClientConfigurations = await ReadClientConfigurationsAsync(legacyConnection, cancellationToken).ConfigureAwait(false);
        var legacyDefinitions = await ReadWearPartDefinitionsAsync(legacyConnection, cancellationToken).ConfigureAwait(false);
        var legacyReplacementRecords = await ReadReplacementRecordsAsync(legacyConnection, cancellationToken).ConfigureAwait(false);
        var legacyExceedLimitRecords = await ReadExceedLimitRecordsAsync(legacyConnection, cancellationToken).ConfigureAwait(false);

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var trackedConfigurations = await dbContext.ClientAppConfigurations
            .Include(x => x.WearPartDefinitions)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var configurationMap = new Dictionary<string, ClientAppConfigurationEntity>(StringComparer.OrdinalIgnoreCase);
        var legacyConfigurationIdMap = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);

        foreach (var trackedConfiguration in trackedConfigurations)
        {
            if (!string.IsNullOrWhiteSpace(trackedConfiguration.ResourceNumber))
            {
                configurationMap[trackedConfiguration.ResourceNumber] = trackedConfiguration;
            }
        }

        foreach (var legacyConfiguration in legacyClientConfigurations)
        {
            if (string.IsNullOrWhiteSpace(legacyConfiguration.ResourceNumber))
            {
                result.SkippedRows++;
                continue;
            }

            if (!configurationMap.TryGetValue(legacyConfiguration.ResourceNumber, out var configuration))
            {
                configuration = new ClientAppConfigurationEntity
                {
                    ResourceNumber = legacyConfiguration.ResourceNumber
                };

                ApplyClientConfiguration(configuration, legacyConfiguration);
                dbContext.ClientAppConfigurations.Add(configuration);
                configurationMap[legacyConfiguration.ResourceNumber] = configuration;
                result.ImportedClientConfigurations++;
            }
            else
            {
                ApplyClientConfiguration(configuration, legacyConfiguration);
                result.UpdatedClientConfigurations++;
            }

            legacyConfigurationIdMap[legacyConfiguration.Id] = configuration.Id;
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var trackedDefinitions = await dbContext.WearPartDefinitions
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var definitionMap = trackedDefinitions.ToDictionary(
            x => BuildDefinitionKey(x.ClientAppConfigurationId, x.PartName),
            x => x,
            StringComparer.OrdinalIgnoreCase);

        var legacyDefinitionIdMap = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);

        foreach (var legacyDefinition in legacyDefinitions)
        {
            if (!legacyConfigurationIdMap.TryGetValue(legacyDefinition.BasicModelId, out var clientAppConfigurationId)
                || string.IsNullOrWhiteSpace(legacyDefinition.Name))
            {
                result.SkippedRows++;
                continue;
            }

            var key = BuildDefinitionKey(clientAppConfigurationId, legacyDefinition.Name);
            if (!definitionMap.TryGetValue(key, out var definition))
            {
                definition = new WearPartDefinitionEntity
                {
                    ClientAppConfigurationId = clientAppConfigurationId,
                    PartName = legacyDefinition.Name.Trim()
                };

                ApplyWearPartDefinition(definition, legacyDefinition, configurationMap.Values.First(x => x.Id == clientAppConfigurationId).ResourceNumber);
                dbContext.WearPartDefinitions.Add(definition);
                definitionMap[key] = definition;
                result.ImportedWearPartDefinitions++;
            }
            else
            {
                ApplyWearPartDefinition(definition, legacyDefinition, configurationMap.Values.First(x => x.Id == clientAppConfigurationId).ResourceNumber);
                result.UpdatedWearPartDefinitions++;
            }

            legacyDefinitionIdMap[legacyDefinition.Id] = definition.Id;
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var importedDefinitionIds = legacyConfigurationIdMap.Values.ToHashSet();
        var existingReplacementKeys = await dbContext.WearPartReplacementRecords
            .Where(x => importedDefinitionIds.Contains(x.ClientAppConfigurationId))
            .Select(x => new { x.WearPartDefinitionId, x.NewBarcode, x.ReplacedAt, x.OperatorWorkNumber })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var replacementKeySet = existingReplacementKeys
            .Select(x => BuildReplacementKey(x.WearPartDefinitionId, x.NewBarcode, x.ReplacedAt, x.OperatorWorkNumber))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var legacyReplacementRecord in legacyReplacementRecords)
        {
            if (!legacyConfigurationIdMap.TryGetValue(legacyReplacementRecord.BasicModelId, out var clientAppConfigurationId))
            {
                result.SkippedRows++;
                continue;
            }

            var definitionKey = BuildDefinitionKey(clientAppConfigurationId, legacyReplacementRecord.Name);
            if (!definitionMap.TryGetValue(definitionKey, out var definition))
            {
                result.SkippedRows++;
                continue;
            }

            var replacementKey = BuildReplacementKey(definition.Id, legacyReplacementRecord.NewBarcode, legacyReplacementRecord.ReplacedAt, legacyReplacementRecord.OperatorWorkNumber);
            if (!replacementKeySet.Add(replacementKey))
            {
                continue;
            }

            dbContext.WearPartReplacementRecords.Add(new WearPartReplacementRecordEntity
            {
                ClientAppConfigurationId = clientAppConfigurationId,
                WearPartDefinitionId = definition.Id,
                SiteCode = NormalizeOrEmpty(legacyReplacementRecord.Site),
                PartName = definition.PartName,
                CurrentBarcode = NormalizeNullable(legacyReplacementRecord.CurrentBarcode),
                NewBarcode = NormalizeOrEmpty(legacyReplacementRecord.NewBarcode),
                CurrentValue = NormalizeOrEmpty(legacyReplacementRecord.CurrentValue),
                WarningValue = NormalizeOrEmpty(legacyReplacementRecord.WarningValue),
                ShutdownValue = NormalizeOrEmpty(legacyReplacementRecord.ShutdownValue),
                OperatorWorkNumber = NormalizeOrEmpty(legacyReplacementRecord.OperatorWorkNumber),
                OperatorUserName = NormalizeOrEmpty(legacyReplacementRecord.OperatorUserName),
                ReplacementReason = NormalizeOrEmpty(legacyReplacementRecord.ReplacementReason),
                ReplacementMessage = NormalizeOrEmpty(legacyReplacementRecord.ReplacementMessage),
                ReplacedAt = legacyReplacementRecord.ReplacedAt,
                DataType = NormalizeNullable(legacyReplacementRecord.DataType),
                DataValue = NormalizeNullable(legacyReplacementRecord.DataValue),
                CreatedAt = legacyReplacementRecord.ReplacedAt,
                UpdatedAt = legacyReplacementRecord.ReplacedAt
            });

            result.ImportedReplacementRecords++;
        }

        var existingExceedKeys = await dbContext.ExceedLimitRecords
            .Where(x => importedDefinitionIds.Contains(x.ClientAppConfigurationId))
            .Select(x => new { x.WearPartDefinitionId, x.OccurredAt, x.CurrentValue, x.ShutdownValue, x.Severity })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var exceedKeySet = existingExceedKeys
            .Select(x => BuildExceedKey(x.WearPartDefinitionId, x.OccurredAt, x.CurrentValue, x.ShutdownValue, x.Severity))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var legacyExceedLimitRecord in legacyExceedLimitRecords)
        {
            if (!legacyConfigurationIdMap.TryGetValue(legacyExceedLimitRecord.BasicId, out var clientAppConfigurationId))
            {
                result.SkippedRows++;
                continue;
            }

            var definitionKey = BuildDefinitionKey(clientAppConfigurationId, legacyExceedLimitRecord.Name);
            if (!definitionMap.TryGetValue(definitionKey, out var definition))
            {
                result.SkippedRows++;
                continue;
            }

            var exceedKey = BuildExceedKey(definition.Id, legacyExceedLimitRecord.OccurredAt, legacyExceedLimitRecord.CurrentValue, legacyExceedLimitRecord.ShutdownValue, ShutdownSeverity);
            if (!exceedKeySet.Add(exceedKey))
            {
                continue;
            }

            dbContext.ExceedLimitRecords.Add(new ExceedLimitRecordEntity
            {
                ClientAppConfigurationId = clientAppConfigurationId,
                WearPartDefinitionId = definition.Id,
                PartName = definition.PartName,
                CurrentValue = legacyExceedLimitRecord.CurrentValue,
                WarningValue = definition.IsShutdown ? 0d : 0d,
                ShutdownValue = legacyExceedLimitRecord.ShutdownValue,
                Severity = ShutdownSeverity,
                OccurredAt = legacyExceedLimitRecord.OccurredAt,
                NotificationMessage = $"旧版数据库导入：资源号 {configurationMap.Values.First(x => x.Id == clientAppConfigurationId).ResourceNumber} 的易损件 {definition.PartName} 当前值 {legacyExceedLimitRecord.CurrentValue}，停机值 {legacyExceedLimitRecord.ShutdownValue}。",
                CreatedAt = legacyExceedLimitRecord.OccurredAt,
                UpdatedAt = legacyExceedLimitRecord.OccurredAt
            });

            result.ImportedExceedLimitRecords++;
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return result;
    }

    public async Task<LegacyDatabaseImportResult> ImportWearPartDefinitionsAsync(string legacyDatabasePath, Guid clientAppConfigurationId, string resourceNumber, CancellationToken cancellationToken = default)
    {
        if (clientAppConfigurationId == Guid.Empty)
        {
            throw new UserFriendlyException("当前客户端未配置有效的客户端信息，无法导入旧库易损件。");
        }

        if (string.IsNullOrWhiteSpace(resourceNumber))
        {
            throw new UserFriendlyException("当前客户端未配置资源号，无法导入旧库易损件。");
        }

        var fullPath = ValidateLegacyDatabasePath(legacyDatabasePath);
        var result = new LegacyDatabaseImportResult
        {
            LegacyDatabasePath = fullPath
        };

        await using var legacyConnection = new SqliteConnection($"Data Source={fullPath}");
        await legacyConnection.OpenAsync(cancellationToken).ConfigureAwait(false);
        var legacyDefinitions = await ReadWearPartDefinitionsAsync(legacyConnection, cancellationToken).ConfigureAwait(false);

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var clientConfigurationExists = await dbContext.ClientAppConfigurations
            .AnyAsync(x => x.Id == clientAppConfigurationId, cancellationToken)
            .ConfigureAwait(false);
        if (!clientConfigurationExists)
        {
            throw new UserFriendlyException("当前客户端基础配置不存在，无法导入旧库易损件。");
        }

        var trackedDefinitions = await dbContext.WearPartDefinitions
            .Where(x => x.ClientAppConfigurationId == clientAppConfigurationId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var definitionMap = trackedDefinitions.ToDictionary(
            x => NormalizeOrEmpty(x.PartName),
            x => x,
            StringComparer.OrdinalIgnoreCase);

        foreach (var legacyDefinition in legacyDefinitions)
        {
            if (string.IsNullOrWhiteSpace(legacyDefinition.Name))
            {
                result.SkippedRows++;
                continue;
            }

            var key = NormalizeOrEmpty(legacyDefinition.Name);
            if (!definitionMap.TryGetValue(key, out var definition))
            {
                definition = new WearPartDefinitionEntity
                {
                    ClientAppConfigurationId = clientAppConfigurationId,
                    ResourceNumber = NormalizeOrEmpty(resourceNumber),
                    PartName = key
                };

                ApplyWearPartDefinition(definition, legacyDefinition, resourceNumber);
                dbContext.WearPartDefinitions.Add(definition);
                definitionMap[key] = definition;
                result.ImportedWearPartDefinitions++;
            }
            else
            {
                ApplyWearPartDefinition(definition, legacyDefinition, resourceNumber);
                result.UpdatedWearPartDefinitions++;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return result;
    }

    private static void ApplyClientConfiguration(ClientAppConfigurationEntity target, LegacyClientConfiguration source)
    {
        target.SiteCode = NormalizeOrEmpty(source.Site);
        target.FactoryCode = NormalizeOrEmpty(source.Factory);
        target.AreaCode = NormalizeOrEmpty(source.Area);
        target.ProcedureCode = NormalizeOrEmpty(source.Procedure);
        target.EquipmentCode = NormalizeOrEmpty(source.EquipmentNum, "000");
        target.ResourceNumber = NormalizeOrEmpty(source.ResourceNumber);
        target.PlcProtocolType = NormalizeOrEmpty(source.PlcType, "S7");
        target.PlcIpAddress = NormalizeOrEmpty(source.PlcIp, "127.0.0.1");
        target.PlcPort = source.Port > 0 ? source.Port : 102;
        target.ShutdownPointAddress = NormalizeOrEmpty(source.ShutdownPoint, "######");
        target.SiemensRack = 0;
        target.SiemensSlot = source.SiemensSlot;
        target.IsStringReverse = source.IsStringReverse;
    }

    private static void ApplyWearPartDefinition(WearPartDefinitionEntity target, LegacyWearPartDefinition source, string resourceNumber)
    {
        target.ResourceNumber = NormalizeOrEmpty(resourceNumber);
        target.PartName = NormalizeOrEmpty(source.Name);
        target.InputMode = NormalizeOrEmpty(source.InputMode, "Barcode");
        target.CurrentValueAddress = NormalizeOrEmpty(source.CurrentValueAddress, "######");
        target.CurrentValueDataType = NormalizeOrEmpty(source.CurrentValueDataType, "String");
        target.WarningValueAddress = NormalizeOrEmpty(source.WarningValueAddress, "######");
        target.WarningValueDataType = NormalizeOrEmpty(source.WarningValueDataType, "String");
        target.ShutdownValueAddress = NormalizeOrEmpty(source.ShutdownValueAddress, "######");
        target.ShutdownValueDataType = NormalizeOrEmpty(source.ShutdownValueDataType, "String");
        target.IsShutdown = source.IsShutdown;
        target.CodeMinLength = source.CodeMinLength > 0 ? source.CodeMinLength : 1;
        target.CodeMaxLength = source.CodeMaxLength >= target.CodeMinLength ? source.CodeMaxLength : Math.Max(target.CodeMinLength, 128);
        target.LifetimeType = NormalizeLifetimeType(source.LifetimeType);
        target.PlcZeroClearAddress = NormalizeOrEmpty(source.PlcZeroClearAddress);
        target.BarcodeWriteAddress = NormalizeOrEmpty(source.BarcodeWriteAddress, "######");
    }

    private static string ValidateLegacyDatabasePath(string legacyDatabasePath)
    {
        if (string.IsNullOrWhiteSpace(legacyDatabasePath))
        {
            throw new UserFriendlyException("旧版 SQLite 数据库文件路径不能为空。");
        }

        var fullPath = Path.GetFullPath(legacyDatabasePath);
        if (!File.Exists(fullPath))
        {
            throw new UserFriendlyException($"未找到旧版 SQLite 数据库文件：{fullPath}");
        }

        return fullPath;
    }

    private static string BuildDefinitionKey(Guid clientAppConfigurationId, string partName)
    {
        return $"{clientAppConfigurationId:N}|{NormalizeOrEmpty(partName)}";
    }

    private static string BuildReplacementKey(Guid definitionId, string newBarcode, DateTime replacedAt, string operatorWorkNumber)
    {
        return $"{definitionId:N}|{NormalizeOrEmpty(newBarcode)}|{replacedAt:O}|{NormalizeOrEmpty(operatorWorkNumber)}";
    }

    private static string BuildExceedKey(Guid definitionId, DateTime occurredAt, double currentValue, double shutdownValue, string severity)
    {
        return $"{definitionId:N}|{occurredAt:O}|{currentValue}|{shutdownValue}|{severity}";
    }

    private static async Task<List<LegacyClientConfiguration>> ReadClientConfigurationsAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        const string sql = "SELECT Id, Site, Factory, Area, Procedure, EquipmentNum, ResourceNum, PlcType, PlcIp, Port, ShutdownPoint, SiemensSlot, IsStringReverse FROM v_Basic";
        return await ExecuteReaderAsync(connection, sql, reader => new LegacyClientConfiguration
        {
            Id = GetString(reader, "Id"),
            Site = GetString(reader, "Site"),
            Factory = GetString(reader, "Factory"),
            Area = GetString(reader, "Area"),
            Procedure = GetString(reader, "Procedure"),
            EquipmentNum = GetString(reader, "EquipmentNum"),
            ResourceNumber = GetString(reader, "ResourceNum"),
            PlcType = GetString(reader, "PlcType"),
            PlcIp = GetString(reader, "PlcIp"),
            Port = GetInt32(reader, "Port"),
            ShutdownPoint = GetString(reader, "ShutdownPoint"),
            SiemensSlot = GetInt32(reader, "SiemensSlot"),
            IsStringReverse = GetBoolean(reader, "IsStringReverse")
        }, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<List<LegacyWearPartDefinition>> ReadWearPartDefinitionsAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        const string sql = "SELECT Id, BasicModelId, Name, Input, CurrentValuePoint, CurrentValueDataType, WarnValuePoint, WarnValueDataType, ShutdownValuePoint, ShutdownValueDataType, IsShutdown, CodeMinLength, CodeMaxLength, LifeType, PlcZeroClear, CodeWritePlcPoint FROM v_VulnerableParts";
        return await ExecuteReaderAsync(connection, sql, reader => new LegacyWearPartDefinition
        {
            Id = GetString(reader, "Id"),
            BasicModelId = GetString(reader, "BasicModelId"),
            Name = GetString(reader, "Name"),
            InputMode = GetString(reader, "Input"),
            CurrentValueAddress = GetString(reader, "CurrentValuePoint"),
            CurrentValueDataType = GetString(reader, "CurrentValueDataType"),
            WarningValueAddress = GetString(reader, "WarnValuePoint"),
            WarningValueDataType = GetString(reader, "WarnValueDataType"),
            ShutdownValueAddress = GetString(reader, "ShutdownValuePoint"),
            ShutdownValueDataType = GetString(reader, "ShutdownValueDataType"),
            IsShutdown = GetBoolean(reader, "IsShutdown"),
            CodeMinLength = GetInt32(reader, "CodeMinLength"),
            CodeMaxLength = GetInt32(reader, "CodeMaxLength"),
            LifetimeType = GetString(reader, "LifeType"),
            PlcZeroClearAddress = GetString(reader, "PlcZeroClear"),
            BarcodeWriteAddress = GetString(reader, "CodeWritePlcPoint")
        }, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<List<LegacyReplacementRecord>> ReadReplacementRecordsAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        const string sql = "SELECT BasicModelId, Site, Name, OldNo, NewNo, CurrentValue, WarnValue, ShutdownValue, OperatorNo, OperatorUser, ReplaceMessage, DateTime, DataType, DataValue FROM v_ReplaceRecord";
        return await ExecuteReaderAsync(connection, sql, reader => new LegacyReplacementRecord
        {
            BasicModelId = GetString(reader, "BasicModelId"),
            Site = GetString(reader, "Site"),
            Name = GetString(reader, "Name"),
            CurrentBarcode = GetString(reader, "OldNo"),
            NewBarcode = GetString(reader, "NewNo"),
            CurrentValue = GetString(reader, "CurrentValue"),
            WarningValue = GetString(reader, "WarnValue"),
            ShutdownValue = GetString(reader, "ShutdownValue"),
            OperatorWorkNumber = GetString(reader, "OperatorNo"),
            OperatorUserName = GetString(reader, "OperatorUser"),
            ReplacementReason = GetString(reader, "ReplaceMessage"),
            ReplacementMessage = GetString(reader, "ReplaceMessage"),
            ReplacedAt = GetDateTime(reader, "DateTime"),
            DataType = GetString(reader, "DataType"),
            DataValue = GetString(reader, "DataValue")
        }, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<List<LegacyExceedLimitRecord>> ReadExceedLimitRecordsAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        const string sql = "SELECT BasicId, Name, CurrentValue, ShutdownValue, DateTime FROM v_exceedlimitinfo";
        return await ExecuteReaderAsync(connection, sql, reader => new LegacyExceedLimitRecord
        {
            BasicId = GetString(reader, "BasicId"),
            Name = GetString(reader, "Name"),
            CurrentValue = GetDouble(reader, "CurrentValue"),
            ShutdownValue = GetDouble(reader, "ShutdownValue"),
            OccurredAt = GetDateTime(reader, "DateTime")
        }, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<List<T>> ExecuteReaderAsync<T>(SqliteConnection connection, string sql, Func<SqliteDataReader, T> materializer, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;

        var results = new List<T>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            results.Add(materializer(reader));
        }

        return results;
    }

    private static string GetString(SqliteDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? string.Empty : Convert.ToString(reader.GetValue(ordinal), System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private static int GetInt32(SqliteDataReader reader, string columnName)
    {
        var raw = GetString(reader, columnName);
        return int.TryParse(raw, out var value) ? value : 0;
    }

    private static double GetDouble(SqliteDataReader reader, string columnName)
    {
        var raw = GetString(reader, columnName);
        return double.TryParse(raw, System.Globalization.NumberStyles.Float | System.Globalization.NumberStyles.AllowThousands, System.Globalization.CultureInfo.InvariantCulture, out var value)
            ? value
            : 0d;
    }

    private static bool GetBoolean(SqliteDataReader reader, string columnName)
    {
        var raw = GetString(reader, columnName);
        return bool.TryParse(raw, out var boolValue)
            ? boolValue
            : raw == "1";
    }

    private static DateTime GetDateTime(SqliteDataReader reader, string columnName)
    {
        var raw = GetString(reader, columnName);
        if (DateTime.TryParse(raw, out var parsed))
        {
            return parsed.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(parsed, DateTimeKind.Utc) : parsed.ToUniversalTime();
        }

        return DateTime.UtcNow;
    }

    private static string NormalizeOrEmpty(string? value, string fallback = "")
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    private static string NormalizeLifetimeType(string? value)
    {
        var normalized = NormalizeOrEmpty(value, "计次");
        return LifetimeTypeAliases.TryGetValue(normalized, out var alias)
            ? alias
            : normalized;
    }

    private static string? NormalizeNullable(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private sealed class LegacyClientConfiguration
    {
        public string Id { get; set; } = string.Empty;
        public string Site { get; set; } = string.Empty;
        public string Factory { get; set; } = string.Empty;
        public string Area { get; set; } = string.Empty;
        public string Procedure { get; set; } = string.Empty;
        public string EquipmentNum { get; set; } = string.Empty;
        public string ResourceNumber { get; set; } = string.Empty;
        public string PlcType { get; set; } = string.Empty;
        public string PlcIp { get; set; } = string.Empty;
        public int Port { get; set; }
        public string ShutdownPoint { get; set; } = string.Empty;
        public int SiemensSlot { get; set; }
        public bool IsStringReverse { get; set; }
    }

    private sealed class LegacyWearPartDefinition
    {
        public string Id { get; set; } = string.Empty;
        public string BasicModelId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string InputMode { get; set; } = string.Empty;
        public string CurrentValueAddress { get; set; } = string.Empty;
        public string CurrentValueDataType { get; set; } = string.Empty;
        public string WarningValueAddress { get; set; } = string.Empty;
        public string WarningValueDataType { get; set; } = string.Empty;
        public string ShutdownValueAddress { get; set; } = string.Empty;
        public string ShutdownValueDataType { get; set; } = string.Empty;
        public bool IsShutdown { get; set; }
        public int CodeMinLength { get; set; }
        public int CodeMaxLength { get; set; }
        public string LifetimeType { get; set; } = string.Empty;
        public string PlcZeroClearAddress { get; set; } = string.Empty;
        public string BarcodeWriteAddress { get; set; } = string.Empty;
    }

    private sealed class LegacyReplacementRecord
    {
        public string BasicModelId { get; set; } = string.Empty;
        public string Site { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string CurrentBarcode { get; set; } = string.Empty;
        public string NewBarcode { get; set; } = string.Empty;
        public string CurrentValue { get; set; } = string.Empty;
        public string WarningValue { get; set; } = string.Empty;
        public string ShutdownValue { get; set; } = string.Empty;
        public string OperatorWorkNumber { get; set; } = string.Empty;
        public string OperatorUserName { get; set; } = string.Empty;
        public string ReplacementReason { get; set; } = string.Empty;
        public string ReplacementMessage { get; set; } = string.Empty;
        public DateTime ReplacedAt { get; set; }
        public string? DataType { get; set; }
        public string? DataValue { get; set; }
    }

    private sealed class LegacyExceedLimitRecord
    {
        public string BasicId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public double CurrentValue { get; set; }
        public double ShutdownValue { get; set; }
        public DateTime OccurredAt { get; set; }
    }
}