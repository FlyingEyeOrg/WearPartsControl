using WearPartsControl.ApplicationServices.SaveInfoService;
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
            PrdResponsibleWorkId = config.PrdResponsibleWorkId?.Trim() ?? string.Empty,
            ComAccessToken = config.ComAccessToken?.Trim() ?? string.Empty,
            ComSecret = config.ComSecret?.Trim() ?? string.Empty,
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

    private static UserConfig ApplyLegacySpacerValidation(UserConfig config, SpacerValidationOptionsSaveInfo legacyConfig)
    {
        ArgumentNullException.ThrowIfNull(legacyConfig);

        return Normalize(new UserConfig
        {
            MeResponsibleWorkId = config.MeResponsibleWorkId,
            PrdResponsibleWorkId = config.PrdResponsibleWorkId,
            ComAccessToken = config.ComAccessToken,
            ComSecret = config.ComSecret,
            SpacerValidationEnabled = legacyConfig.Enabled,
            SpacerValidationUrl = legacyConfig.ValidationUrl,
            SpacerValidationTimeoutMilliseconds = legacyConfig.TimeoutMilliseconds,
            SpacerValidationIgnoreServerCertificateErrors = legacyConfig.IgnoreServerCertificateErrors,
            SpacerValidationCodeSeparator = legacyConfig.CodeSeparator,
            SpacerValidationExpectedSegmentCount = legacyConfig.ExpectedSegmentCount
        });
    }
}