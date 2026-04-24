using System;
using System.Globalization;
using System.Linq;
using System.Resources;
using System.Threading;
using System.Threading.Tasks;
using WearPartsControl.ApplicationServices.Localization.Generated;
using WearPartsControl.ApplicationServices.SaveInfoService;
using WearPartsControl.ApplicationServices.UserConfig;
using UserConfigModel = WearPartsControl.ApplicationServices.UserConfig.UserConfig;

namespace WearPartsControl.ApplicationServices.Localization;

public sealed class LocalizationService : ILocalizationService
{
    private static readonly string[] SupportedCultures = ["zh-CN", "en-US"];
    private static readonly ResourceManager ResourceManager = new("WearPartsControl.Resources.LocalizationResource", typeof(LocalizationService).Assembly);

    private readonly ISaveInfoStore _saveInfoStore;
    private readonly LocalizationCatalog _catalog;

    public LocalizationService(ISaveInfoStore saveInfoStore)
    {
        _saveInfoStore = saveInfoStore;
        _catalog = new LocalizationCatalog(GetString);
    }

    public string this[string name] => GetString(name);

    public LocalizationCatalog Catalog => _catalog;

    public CultureInfo CurrentCulture => CultureInfo.CurrentUICulture;

    public async ValueTask InitializeAsync(CancellationToken cancellationToken = default)
    {
        var userConfig = await _saveInfoStore.ReadAsync<UserConfigModel>(cancellationToken).ConfigureAwait(false);
        var storedCultureName = NormalizeCultureName(userConfig.Language);

        if (string.IsNullOrWhiteSpace(storedCultureName))
        {
            var legacyConfig = await _saveInfoStore.ReadAsync<LocalizationOptionsSaveInfo>(cancellationToken).ConfigureAwait(false);
            storedCultureName = NormalizeCultureName(legacyConfig.CultureName);

            if (!string.IsNullOrWhiteSpace(storedCultureName))
            {
                userConfig.Language = storedCultureName;
                await _saveInfoStore.WriteAsync(userConfig, cancellationToken).ConfigureAwait(false);

                if (_saveInfoStore is TypeJsonSaveInfoStore fileStore && fileStore.Exists<LocalizationOptionsSaveInfo>())
                {
                    await fileStore.DeleteAsync<LocalizationOptionsSaveInfo>(cancellationToken).ConfigureAwait(false);
                }
            }
        }

        await SetCultureAsync(storedCultureName, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask SetCultureAsync(string cultureName, CancellationToken cancellationToken = default)
    {
        cultureName = NormalizeCultureName(cultureName);

        var culture = CultureInfo.GetCultureInfo(cultureName);
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        LocalizationBindingSource.Instance.Refresh();

        var config = new LocalizationOptionsSaveInfo { CultureName = culture.Name };
        await _saveInfoStore.WriteAsync(config, cancellationToken).ConfigureAwait(false);

        var userConfig = await _saveInfoStore.ReadAsync<UserConfigModel>(cancellationToken).ConfigureAwait(false);
        if (!string.Equals(userConfig.Language, culture.Name, StringComparison.Ordinal))
        {
            userConfig.Language = culture.Name;
            await _saveInfoStore.WriteAsync(userConfig, cancellationToken).ConfigureAwait(false);
        }
    }

    private static string NormalizeCultureName(string? cultureName)
    {
        return SupportedCultures.Contains(cultureName ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            ? CultureInfo.GetCultureInfo(cultureName!).Name
                : UserConfigModel.DefaultLanguage;
    }

    private static string GetString(string name)
    {
        return ResourceManager.GetString(name, CultureInfo.CurrentUICulture) ?? name;
    }
}
