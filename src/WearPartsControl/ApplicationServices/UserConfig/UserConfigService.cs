using WearPartsControl.ApplicationServices.SaveInfoService;
using WearPartsControl.ApplicationServices.ComNotification;
using WearPartsControl.ApplicationServices.SpacerManagement;

namespace WearPartsControl.ApplicationServices.UserConfig;

public sealed class UserConfigService : IUserConfigService
{
    private readonly ISaveInfoStore _saveInfoStore;

    public UserConfigService(ISaveInfoStore saveInfoStore)
    {
        _saveInfoStore = saveInfoStore;
    }

    public async ValueTask<UserConfig> GetAsync(CancellationToken cancellationToken = default)
    {
        var config = await _saveInfoStore.ReadAsync<UserConfig>(cancellationToken).ConfigureAwait(false);
        var normalized = Normalize(config);

        if (_saveInfoStore is TypeJsonSaveInfoStore comFileStore && comFileStore.Exists<ComNotificationOptionsSaveInfo>())
        {
            if (ShouldMigrateLegacyComNotification(normalized))
            {
                var legacyComNotification = await _saveInfoStore.ReadAsync<ComNotificationOptionsSaveInfo>(cancellationToken).ConfigureAwait(false);
                normalized = ApplyLegacyComNotification(normalized, legacyComNotification);
                await _saveInfoStore.WriteAsync(normalized, cancellationToken).ConfigureAwait(false);
            }

            await comFileStore.DeleteAsync<ComNotificationOptionsSaveInfo>(cancellationToken).ConfigureAwait(false);
        }

        if (_saveInfoStore is TypeJsonSaveInfoStore fileStore && fileStore.Exists<SpacerValidationOptionsSaveInfo>())
        {
            if (ShouldMigrateLegacySpacerValidation(normalized))
            {
                var legacyConfig = await _saveInfoStore.ReadAsync<SpacerValidationOptionsSaveInfo>(cancellationToken).ConfigureAwait(false);
                normalized = ApplyLegacySpacerValidation(normalized, legacyConfig);
                await _saveInfoStore.WriteAsync(normalized, cancellationToken).ConfigureAwait(false);
            }

            await fileStore.DeleteAsync<SpacerValidationOptionsSaveInfo>(cancellationToken).ConfigureAwait(false);
        }

        return normalized;
    }

    public async ValueTask SaveAsync(UserConfig config, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(config);
        await _saveInfoStore.WriteAsync(Normalize(config), cancellationToken).ConfigureAwait(false);

        if (_saveInfoStore is TypeJsonSaveInfoStore comFileStore && comFileStore.Exists<ComNotificationOptionsSaveInfo>())
        {
            await comFileStore.DeleteAsync<ComNotificationOptionsSaveInfo>(cancellationToken).ConfigureAwait(false);
        }

        if (_saveInfoStore is TypeJsonSaveInfoStore fileStore && fileStore.Exists<SpacerValidationOptionsSaveInfo>())
        {
            await fileStore.DeleteAsync<SpacerValidationOptionsSaveInfo>(cancellationToken).ConfigureAwait(false);
        }
    }

    private static UserConfig Normalize(UserConfig config)
    {
        return new UserConfig
        {
            MeResponsibleWorkId = config.MeResponsibleWorkId?.Trim() ?? string.Empty,
            MeResponsibleName = config.MeResponsibleName?.Trim() ?? string.Empty,
            PrdResponsibleWorkId = config.PrdResponsibleWorkId?.Trim() ?? string.Empty,
            PrdResponsibleName = config.PrdResponsibleName?.Trim() ?? string.Empty,
            ReplacementOperatorName = config.ReplacementOperatorName?.Trim() ?? string.Empty,
            ComAccessToken = config.ComAccessToken?.Trim() ?? string.Empty,
            ComSecret = config.ComSecret?.Trim() ?? string.Empty,
            ComNotificationEnabled = config.ComNotificationEnabled,
            ComPushUrl = string.IsNullOrWhiteSpace(config.ComPushUrl)
                ? UserConfig.DefaultComPushUrl
                : config.ComPushUrl.Trim(),
            ComDeIpaasKeyAuth = string.IsNullOrWhiteSpace(config.ComDeIpaasKeyAuth)
                ? UserConfig.DefaultComDeIpaasKeyAuth
                : config.ComDeIpaasKeyAuth.Trim(),
            ComAgentId = config.ComAgentId > 0 ? config.ComAgentId : UserConfig.DefaultComAgentId,
            ComGroupTemplateId = config.ComGroupTemplateId > 0 ? config.ComGroupTemplateId : UserConfig.DefaultComGroupTemplateId,
            ComWorkTemplateId = config.ComWorkTemplateId > 0 ? config.ComWorkTemplateId : UserConfig.DefaultComWorkTemplateId,
            ComUserType = string.IsNullOrWhiteSpace(config.ComUserType)
                ? UserConfig.DefaultComUserType
                : config.ComUserType.Trim(),
            ComTimeoutMilliseconds = config.ComTimeoutMilliseconds > 0
                ? config.ComTimeoutMilliseconds
                : UserConfig.DefaultComTimeoutMilliseconds,
            SpacerValidationEnabled = config.SpacerValidationEnabled,
            SpacerValidationUrl = config.SpacerValidationUrl?.Trim() ?? string.Empty,
            SpacerValidationTimeoutMilliseconds = config.SpacerValidationTimeoutMilliseconds > 0
                ? config.SpacerValidationTimeoutMilliseconds
                : UserConfig.DefaultSpacerValidationTimeoutMilliseconds,
            SpacerValidationIgnoreServerCertificateErrors = config.SpacerValidationIgnoreServerCertificateErrors,
            SpacerValidationCodeSeparator = string.IsNullOrWhiteSpace(config.SpacerValidationCodeSeparator)
                ? UserConfig.DefaultSpacerValidationCodeSeparator
                : config.SpacerValidationCodeSeparator.Trim(),
            SpacerValidationExpectedSegmentCount = config.SpacerValidationExpectedSegmentCount > 0
                ? config.SpacerValidationExpectedSegmentCount
                : UserConfig.DefaultSpacerValidationExpectedSegmentCount
        };
    }

    private static bool ShouldMigrateLegacySpacerValidation(UserConfig config)
    {
        return string.IsNullOrWhiteSpace(config.SpacerValidationUrl)
            && config.SpacerValidationEnabled
            && config.SpacerValidationTimeoutMilliseconds == UserConfig.DefaultSpacerValidationTimeoutMilliseconds
            && config.SpacerValidationIgnoreServerCertificateErrors
            && string.Equals(config.SpacerValidationCodeSeparator, UserConfig.DefaultSpacerValidationCodeSeparator, StringComparison.Ordinal)
            && config.SpacerValidationExpectedSegmentCount == UserConfig.DefaultSpacerValidationExpectedSegmentCount;
    }

    private static bool ShouldMigrateLegacyComNotification(UserConfig config)
    {
        return config.ComNotificationEnabled == UserConfig.DefaultComNotificationEnabled
            && string.IsNullOrWhiteSpace(config.ComAccessToken)
            && string.IsNullOrWhiteSpace(config.ComSecret)
            && string.IsNullOrWhiteSpace(config.MeResponsibleWorkId)
            && string.IsNullOrWhiteSpace(config.MeResponsibleName)
            && string.IsNullOrWhiteSpace(config.PrdResponsibleWorkId)
            && string.IsNullOrWhiteSpace(config.PrdResponsibleName)
            && string.IsNullOrWhiteSpace(config.ReplacementOperatorName)
            && string.Equals(config.ComPushUrl, UserConfig.DefaultComPushUrl, StringComparison.Ordinal)
            && string.Equals(config.ComDeIpaasKeyAuth, UserConfig.DefaultComDeIpaasKeyAuth, StringComparison.Ordinal)
            && config.ComAgentId == UserConfig.DefaultComAgentId
            && config.ComGroupTemplateId == UserConfig.DefaultComGroupTemplateId
            && config.ComWorkTemplateId == UserConfig.DefaultComWorkTemplateId
            && string.Equals(config.ComUserType, UserConfig.DefaultComUserType, StringComparison.Ordinal)
            && config.ComTimeoutMilliseconds == UserConfig.DefaultComTimeoutMilliseconds;
    }

    private static UserConfig ApplyLegacyComNotification(UserConfig config, ComNotificationOptionsSaveInfo legacyConfig)
    {
        ArgumentNullException.ThrowIfNull(legacyConfig);

        return Normalize(new UserConfig
        {
            MeResponsibleWorkId = string.IsNullOrWhiteSpace(config.MeResponsibleWorkId)
                ? legacyConfig.DefaultUserWorkId
                : config.MeResponsibleWorkId,
            MeResponsibleName = config.MeResponsibleName,
            PrdResponsibleWorkId = config.PrdResponsibleWorkId,
            PrdResponsibleName = config.PrdResponsibleName,
            ReplacementOperatorName = config.ReplacementOperatorName,
            ComAccessToken = string.IsNullOrWhiteSpace(config.ComAccessToken)
                ? legacyConfig.AccessToken
                : config.ComAccessToken,
            ComSecret = string.IsNullOrWhiteSpace(config.ComSecret)
                ? legacyConfig.Secret
                : config.ComSecret,
            ComNotificationEnabled = legacyConfig.Enabled,
            ComPushUrl = legacyConfig.PushUrl,
            ComDeIpaasKeyAuth = legacyConfig.DeIpaasKeyAuth,
            ComAgentId = legacyConfig.AgentId,
            ComGroupTemplateId = legacyConfig.GroupTemplateId,
            ComWorkTemplateId = legacyConfig.WorkTemplateId,
            ComUserType = legacyConfig.UserType,
            ComTimeoutMilliseconds = legacyConfig.TimeoutMilliseconds,
            SpacerValidationEnabled = config.SpacerValidationEnabled,
            SpacerValidationUrl = config.SpacerValidationUrl,
            SpacerValidationTimeoutMilliseconds = config.SpacerValidationTimeoutMilliseconds,
            SpacerValidationIgnoreServerCertificateErrors = config.SpacerValidationIgnoreServerCertificateErrors,
            SpacerValidationCodeSeparator = config.SpacerValidationCodeSeparator,
            SpacerValidationExpectedSegmentCount = config.SpacerValidationExpectedSegmentCount
        });
    }

    private static UserConfig ApplyLegacySpacerValidation(UserConfig config, SpacerValidationOptionsSaveInfo legacyConfig)
    {
        ArgumentNullException.ThrowIfNull(legacyConfig);

        return Normalize(new UserConfig
        {
            MeResponsibleWorkId = config.MeResponsibleWorkId,
            MeResponsibleName = config.MeResponsibleName,
            PrdResponsibleWorkId = config.PrdResponsibleWorkId,
            PrdResponsibleName = config.PrdResponsibleName,
            ReplacementOperatorName = config.ReplacementOperatorName,
            ComAccessToken = config.ComAccessToken,
            ComSecret = config.ComSecret,
            ComNotificationEnabled = config.ComNotificationEnabled,
            ComPushUrl = config.ComPushUrl,
            ComDeIpaasKeyAuth = config.ComDeIpaasKeyAuth,
            ComAgentId = config.ComAgentId,
            ComGroupTemplateId = config.ComGroupTemplateId,
            ComWorkTemplateId = config.ComWorkTemplateId,
            ComUserType = config.ComUserType,
            ComTimeoutMilliseconds = config.ComTimeoutMilliseconds,
            SpacerValidationEnabled = legacyConfig.Enabled,
            SpacerValidationUrl = legacyConfig.ValidationUrl,
            SpacerValidationTimeoutMilliseconds = legacyConfig.TimeoutMilliseconds,
            SpacerValidationIgnoreServerCertificateErrors = legacyConfig.IgnoreServerCertificateErrors,
            SpacerValidationCodeSeparator = legacyConfig.CodeSeparator,
            SpacerValidationExpectedSegmentCount = legacyConfig.ExpectedSegmentCount
        });
    }
}