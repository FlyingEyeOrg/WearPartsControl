using System.IO;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using WearPartsControl.ApplicationServices.AppSettings;
using WearPartsControl.ApplicationServices.ClientAppInfo;
using WearPartsControl.ApplicationServices.Localization;
using WearPartsControl.ApplicationServices.LoginService;
using WearPartsControl.ApplicationServices.SaveInfoService;
using WearPartsControl.ApplicationServices.SpacerManagement;
using WearPartsControl.ApplicationServices.UserConfig;
using WearPartsControl.Exceptions;
using UserConfigModel = WearPartsControl.ApplicationServices.UserConfig.UserConfig;

namespace WearPartsControl.ApplicationServices.LegacyImport;

public sealed class LegacyConfigurationImportService : ILegacyConfigurationImportService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private readonly IClientAppInfoService _clientAppInfoService;
    private readonly IAppSettingsService _appSettingsService;
    private readonly ISaveInfoStore _saveInfoStore;

    public LegacyConfigurationImportService(
        IClientAppInfoService clientAppInfoService,
        IAppSettingsService appSettingsService,
        ISaveInfoStore saveInfoStore)
    {
        _clientAppInfoService = clientAppInfoService;
        _appSettingsService = appSettingsService;
        _saveInfoStore = saveInfoStore;
    }

    public async Task<LegacyConfigurationImportResult> ImportAsync(string legacyDatabasePath, CancellationToken cancellationToken = default)
    {
        var fullPath = ValidateLegacyDatabasePath(legacyDatabasePath);
        var legacyRootDirectory = ResolveLegacyRootDirectory(fullPath);
        var legacyJsonDirectory = Path.Combine(legacyRootDirectory, "Json");

        var legacyAppSetting = await ReadJsonAsync<LegacyAppSetting>(Path.Combine(legacyJsonDirectory, "AppSetting.json"), cancellationToken).ConfigureAwait(false);
        var legacyAppConfig = await ReadJsonAsync<LegacyAppConfig>(Path.Combine(legacyJsonDirectory, "AppConfig.json"), cancellationToken).ConfigureAwait(false);
        var legacyComNotification = await ReadLegacyComNotificationAsync(legacyJsonDirectory, legacyRootDirectory, cancellationToken).ConfigureAwait(false);
        var legacySpacerValidation = await ReadLegacySpacerValidationAsync(legacyJsonDirectory, legacyRootDirectory, cancellationToken).ConfigureAwait(false);
        var legacyMhrInfos = await ReadJsonAsync<LegacyMhrInfosConfig>(Path.Combine(legacyJsonDirectory, "MHRInfos.json"), cancellationToken).ConfigureAwait(false);
        var legacyMhr = legacyMhrInfos is null
            ? await ReadJsonAsync<LegacyMhrConfig>(Path.Combine(legacyJsonDirectory, "MHR.json"), cancellationToken).ConfigureAwait(false)
            : null;

        var expectedResourceNumber = Normalize(legacyAppSetting?.ResourceNum);
        var legacyClientConfiguration = await ReadLegacyClientConfigurationAsync(fullPath, expectedResourceNumber, cancellationToken).ConfigureAwait(false);

        var request = new ClientAppInfoSaveRequest
        {
            SiteCode = Normalize(legacyClientConfiguration.Site),
            FactoryCode = Normalize(legacyClientConfiguration.Factory),
            AreaCode = Normalize(legacyClientConfiguration.Area),
            ProcedureCode = Normalize(legacyClientConfiguration.Procedure),
            EquipmentCode = Normalize(legacyClientConfiguration.EquipmentNum, "000"),
            ResourceNumber = Normalize(legacyClientConfiguration.ResourceNumber, expectedResourceNumber),
            PlcProtocolType = LegacyImportValueConverter.NormalizePlcProtocolType(legacyClientConfiguration.PlcType),
            PlcIpAddress = Normalize(legacyClientConfiguration.PlcIp, "127.0.0.1"),
            PlcPort = legacyClientConfiguration.Port > 0 ? legacyClientConfiguration.Port : 102,
            ShutdownPointAddress = Normalize(legacyClientConfiguration.ShutdownPoint, "######"),
            SiemensRack = 0,
            SiemensSlot = legacyClientConfiguration.SiemensSlot >= 0 ? legacyClientConfiguration.SiemensSlot : 0,
            IsStringReverse = legacyClientConfiguration.IsStringReverse
        };

        var importedClientAppInfo = await _clientAppInfoService.SaveAsync(request, cancellationToken).ConfigureAwait(false);

        var result = new LegacyConfigurationImportResult
        {
            LegacyDatabasePath = fullPath,
            LegacyRootDirectory = legacyRootDirectory,
            ResourceNumber = importedClientAppInfo.ResourceNumber,
            ClientAppInfo = importedClientAppInfo,
            ImportedAppSettings = true
        };

        var appSettings = await _appSettingsService.GetAsync(cancellationToken).ConfigureAwait(false);
        appSettings.ResourceNumber = importedClientAppInfo.ResourceNumber;
        appSettings.IsSetClientAppInfo = true;
        if (legacyAppConfig is not null)
        {
            appSettings.UseWorkNumberLogin = legacyAppConfig.UseUserNumber;
        }

        await _appSettingsService.SaveAsync(appSettings, cancellationToken).ConfigureAwait(false);

        if (legacyAppConfig is not null || legacySpacerValidation is not null)
        {
            result.ImportedSpacerValidation = await ImportSpacerValidationAsync(legacyAppConfig, legacySpacerValidation, cancellationToken).ConfigureAwait(false);
        }

        if (legacyAppConfig is not null)
        {
            result.ImportedUserConfig = await ImportUserConfigAsync(legacyAppConfig, cancellationToken).ConfigureAwait(false);
        }

        result.ImportedComNotification = await ImportComNotificationAsync(legacyAppConfig, legacyComNotification, cancellationToken).ConfigureAwait(false);

        if (legacyMhrInfos is not null || legacyMhr is not null)
        {
            await ImportMhrConfigAsync(legacyMhrInfos, legacyMhr, cancellationToken).ConfigureAwait(false);
            result.ImportedMhrConfig = true;
        }

        return result;
    }

    private async Task<bool> ImportSpacerValidationAsync(LegacyAppConfig? legacyAppConfig, SpacerValidationOptionsSaveInfo? legacySpacerValidation, CancellationToken cancellationToken)
    {
        var userConfig = await _saveInfoStore.ReadAsync<UserConfigModel>(cancellationToken).ConfigureAwait(false);
        var hasChanges = false;

        if (legacySpacerValidation is not null)
        {
            userConfig.SpacerValidationEnabled = legacySpacerValidation.Enabled;
            userConfig.SpacerValidationUrl = Normalize(legacySpacerValidation.ValidationUrl);
            userConfig.SpacerValidationTimeoutMilliseconds = legacySpacerValidation.TimeoutMilliseconds > 0
                ? legacySpacerValidation.TimeoutMilliseconds
                : UserConfigModel.DefaultSpacerValidationTimeoutMilliseconds;
            userConfig.SpacerValidationIgnoreServerCertificateErrors = legacySpacerValidation.IgnoreServerCertificateErrors;
            userConfig.SpacerValidationCodeSeparator = Normalize(legacySpacerValidation.CodeSeparator, UserConfigModel.DefaultSpacerValidationCodeSeparator);
            userConfig.SpacerValidationExpectedSegmentCount = legacySpacerValidation.ExpectedSegmentCount > 0
                ? legacySpacerValidation.ExpectedSegmentCount
                : UserConfigModel.DefaultSpacerValidationExpectedSegmentCount;
            hasChanges = true;
        }
        else
        {
            var validationUrl = Normalize(legacyAppConfig?.SpacerValidationUrl);
            if (!string.IsNullOrWhiteSpace(validationUrl))
            {
                userConfig.SpacerValidationEnabled = true;
                userConfig.SpacerValidationUrl = validationUrl;
                hasChanges = true;
            }
        }

        if (!hasChanges)
        {
            return false;
        }

        await _saveInfoStore.WriteAsync(userConfig, cancellationToken).ConfigureAwait(false);
        return true;
    }

    private static async Task<SpacerValidationOptionsSaveInfo?> ReadLegacySpacerValidationAsync(string legacyJsonDirectory, string legacyRootDirectory, CancellationToken cancellationToken)
    {
        foreach (var path in new[]
                 {
                     Path.Combine(legacyJsonDirectory, "spacer-validation.json"),
                     Path.Combine(legacyRootDirectory, "spacer-validation.json"),
                     Path.Combine(legacyRootDirectory, "PrivateData", "Settings", "spacer-validation.json")
                 })
        {
            var config = await ReadJsonAsync<SpacerValidationOptionsSaveInfo>(path, cancellationToken).ConfigureAwait(false);
            if (config is not null)
            {
                return config;
            }
        }

        return null;
    }

    private static async Task<LegacyComNotificationConfig?> ReadLegacyComNotificationAsync(string legacyJsonDirectory, string legacyRootDirectory, CancellationToken cancellationToken)
    {
        foreach (var path in new[]
                 {
                     Path.Combine(legacyJsonDirectory, "com-notification.json"),
                     Path.Combine(legacyRootDirectory, "com-notification.json"),
                     Path.Combine(legacyRootDirectory, "PrivateData", "Settings", "com-notification.json")
                 })
        {
            var config = await ReadJsonAsync<LegacyComNotificationConfig>(path, cancellationToken).ConfigureAwait(false);
            if (config is not null)
            {
                return config;
            }
        }

        return null;
    }

    private async Task<bool> ImportUserConfigAsync(LegacyAppConfig legacyAppConfig, CancellationToken cancellationToken)
    {
        var userConfig = await _saveInfoStore.ReadAsync<UserConfigModel>(cancellationToken).ConfigureAwait(false);
        var hasChanges = false;

        var userWorkId = Normalize(legacyAppConfig.UserWorkId);
        if (!string.IsNullOrWhiteSpace(userWorkId))
        {
            userConfig.MeResponsibleWorkId = userWorkId;
            hasChanges = true;
        }

        var accessToken = Normalize(legacyAppConfig.AccessToken);
        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            userConfig.ComAccessToken = accessToken;
            hasChanges = true;
        }

        var secret = Normalize(legacyAppConfig.Secret);
        if (!string.IsNullOrWhiteSpace(secret))
        {
            userConfig.ComSecret = secret;
            hasChanges = true;
        }

        if (!hasChanges)
        {
            return false;
        }

        await _saveInfoStore.WriteAsync(userConfig, cancellationToken).ConfigureAwait(false);
        return true;
    }

    private async Task<bool> ImportComNotificationAsync(LegacyAppConfig? legacyAppConfig, LegacyComNotificationConfig? legacyComNotification, CancellationToken cancellationToken)
    {
        var userConfig = await _saveInfoStore.ReadAsync<UserConfigModel>(cancellationToken).ConfigureAwait(false);
        var hasChanges = false;

        var accessToken = Normalize(legacyAppConfig?.AccessToken);
        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            userConfig.ComAccessToken = accessToken;
            hasChanges = true;
        }

        var secret = Normalize(legacyAppConfig?.Secret);
        if (!string.IsNullOrWhiteSpace(secret))
        {
            userConfig.ComSecret = secret;
            hasChanges = true;
        }

        var userWorkId = Normalize(legacyAppConfig?.UserWorkId);
        if (!string.IsNullOrWhiteSpace(userWorkId))
        {
            if (string.IsNullOrWhiteSpace(userConfig.MeResponsibleWorkId))
            {
                userConfig.MeResponsibleWorkId = userWorkId;
                hasChanges = true;
            }
        }

        if (legacyComNotification is not null)
        {
            userConfig.ComNotificationEnabled = legacyComNotification.Enabled;
            userConfig.ComPushUrl = Normalize(legacyComNotification.PushUrl, UserConfigModel.DefaultComPushUrl);
            userConfig.ComDeIpaasKeyAuth = Normalize(legacyComNotification.DeIpaasKeyAuth, UserConfigModel.DefaultComDeIpaasKeyAuth);
            userConfig.ComAgentId = legacyComNotification.AgentId > 0 ? legacyComNotification.AgentId : UserConfigModel.DefaultComAgentId;
            userConfig.ComGroupTemplateId = legacyComNotification.GroupTemplateId > 0 ? legacyComNotification.GroupTemplateId : UserConfigModel.DefaultComGroupTemplateId;
            userConfig.ComWorkTemplateId = legacyComNotification.WorkTemplateId > 0 ? legacyComNotification.WorkTemplateId : UserConfigModel.DefaultComWorkTemplateId;
            userConfig.ComUserType = Normalize(legacyComNotification.UserType, UserConfigModel.DefaultComUserType);
            userConfig.ComTimeoutMilliseconds = legacyComNotification.TimeoutMilliseconds > 0 ? legacyComNotification.TimeoutMilliseconds : UserConfigModel.DefaultComTimeoutMilliseconds;
            hasChanges = true;
        }

        if (!hasChanges)
        {
            return false;
        }

        await _saveInfoStore.WriteAsync(userConfig, cancellationToken).ConfigureAwait(false);
        return true;
    }

    private async Task ImportMhrConfigAsync(LegacyMhrInfosConfig? legacyMhrInfos, LegacyMhrConfig? legacyMhr, CancellationToken cancellationToken)
    {
        var config = new MhrConfig();

        if (legacyMhrInfos is not null)
        {
            config.LoginName = Normalize(legacyMhrInfos.LoginName);
            config.Password = Normalize(legacyMhrInfos.Password);
            config.LoginInfos = legacyMhrInfos.MHRInfos
                .Where(x => x is not null)
                .Select(x => new MhrSiteLoginInfo
                {
                    Site = Normalize(x!.Site),
                    LoginUrl = Normalize(x.LoginUrl),
                    GetUsersUrl = Normalize(x.GetUsersUrl)
                })
                .Where(x => !string.IsNullOrWhiteSpace(x.Site)
                    || !string.IsNullOrWhiteSpace(x.LoginUrl)
                    || !string.IsNullOrWhiteSpace(x.GetUsersUrl))
                .ToList();
        }
        else if (legacyMhr is not null)
        {
            config.LoginName = Normalize(legacyMhr.LoginName);
            config.Password = Normalize(legacyMhr.LoginPassword);
            if (!string.IsNullOrWhiteSpace(legacyMhr.GetTokenUrl) || !string.IsNullOrWhiteSpace(legacyMhr.GetListUrl))
            {
                config.LoginInfos.Add(new MhrSiteLoginInfo
                {
                    Site = string.Empty,
                    LoginUrl = Normalize(legacyMhr.GetTokenUrl),
                    GetUsersUrl = Normalize(legacyMhr.GetListUrl)
                });
            }
        }

        await _saveInfoStore.WriteAsync(config, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<LegacyClientConfiguration> ReadLegacyClientConfigurationAsync(string databasePath, string expectedResourceNumber, CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection($"Data Source={databasePath}");
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        var configurations = await ExecuteReaderAsync(
                connection,
                "SELECT Id, Site, Factory, Area, Procedure, EquipmentNum, ResourceNum, PlcType, PlcIp, Port, ShutdownPoint, SiemensSlot, IsStringReverse FROM v_Basic",
                reader => new LegacyClientConfiguration
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
                },
                cancellationToken)
            .ConfigureAwait(false);

        if (configurations.Count == 0)
        {
            throw new UserFriendlyException(LocalizedText.Get("Services.LegacyImport.ClientConfigMissing"), code: "LegacyConfigImport:ClientConfigMissing");
        }

        if (!string.IsNullOrWhiteSpace(expectedResourceNumber))
        {
            var matched = configurations.FirstOrDefault(x => string.Equals(Normalize(x.ResourceNumber), expectedResourceNumber, StringComparison.OrdinalIgnoreCase));
            if (matched is null)
            {
                throw new UserFriendlyException(LocalizedText.Format("Services.LegacyImport.ResourceNotFound", expectedResourceNumber), code: "LegacyConfigImport:ResourceNotFound");
            }

            return matched;
        }

        return configurations[0];
    }

    private static async Task<T?> ReadJsonAsync<T>(string filePath, CancellationToken cancellationToken) where T : class
    {
        if (!File.Exists(filePath))
        {
            return null;
        }

        await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 16 * 1024, FileOptions.SequentialScan);
        if (stream.Length == 0)
        {
            return null;
        }

        return await JsonSerializer.DeserializeAsync<T>(stream, SerializerOptions, cancellationToken).ConfigureAwait(false);
    }

    private static string ValidateLegacyDatabasePath(string legacyDatabasePath)
    {
        if (string.IsNullOrWhiteSpace(legacyDatabasePath))
        {
            throw new UserFriendlyException(LocalizedText.Get("Services.LegacyImport.PathRequired"), code: "LegacyConfigImport:PathRequired");
        }

        var fullPath = Path.GetFullPath(legacyDatabasePath);
        if (!File.Exists(fullPath))
        {
            throw new UserFriendlyException(LocalizedText.Format("Services.LegacyImport.PathNotFound", fullPath), code: "LegacyConfigImport:PathNotFound");
        }

        return fullPath;
    }

    private static string ResolveLegacyRootDirectory(string legacyDatabasePath)
    {
        var databaseDirectory = Path.GetDirectoryName(legacyDatabasePath);
        if (string.IsNullOrWhiteSpace(databaseDirectory))
        {
            throw new UserFriendlyException(LocalizedText.Get("Services.LegacyImport.InvalidDirectory"), code: "LegacyConfigImport:InvalidDirectory");
        }

        var directoryInfo = new DirectoryInfo(databaseDirectory);
        if (directoryInfo.Name.Equals("db", StringComparison.OrdinalIgnoreCase) && directoryInfo.Parent is not null)
        {
            return directoryInfo.Parent.FullName;
        }

        return directoryInfo.FullName;
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

    private static bool GetBoolean(SqliteDataReader reader, string columnName)
    {
        var raw = GetString(reader, columnName);
        return bool.TryParse(raw, out var value) ? value : raw == "1";
    }

    private static string Normalize(string? value, string fallback = "")
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
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

    private sealed class LegacyAppSetting
    {
        public string ResourceNum { get; set; } = string.Empty;
    }

    private sealed class LegacyAppConfig
    {
        public bool UseUserNumber { get; set; }

        public string SpacerValidationUrl { get; set; } = string.Empty;

        public string HostIPAddress { get; set; } = string.Empty;

        public string UserWorkId { get; set; } = string.Empty;

        public string AccessToken { get; set; } = string.Empty;

        public string Secret { get; set; } = string.Empty;
    }

    private sealed class LegacyMhrInfosConfig
    {
        public string LoginName { get; set; } = string.Empty;

        public string Password { get; set; } = string.Empty;

        public List<LegacyMhrInfo> MHRInfos { get; set; } = new();
    }

    private sealed class LegacyMhrInfo
    {
        public string Site { get; set; } = string.Empty;

        public string LoginUrl { get; set; } = string.Empty;

        public string GetUsersUrl { get; set; } = string.Empty;
    }

    private sealed class LegacyMhrConfig
    {
        public string GetTokenUrl { get; set; } = string.Empty;

        public string LoginName { get; set; } = string.Empty;

        public string LoginPassword { get; set; } = string.Empty;

        public string GetListUrl { get; set; } = string.Empty;
    }

    private sealed class LegacyComNotificationConfig
    {
        public bool Enabled { get; set; }

        public string PushUrl { get; set; } = string.Empty;

        public string DeIpaasKeyAuth { get; set; } = string.Empty;

        public long AgentId { get; set; }

        public long GroupTemplateId { get; set; }

        public long WorkTemplateId { get; set; }

        public string UserType { get; set; } = string.Empty;

        public int TimeoutMilliseconds { get; set; }
    }
}