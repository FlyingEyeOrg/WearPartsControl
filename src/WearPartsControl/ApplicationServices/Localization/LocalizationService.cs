using System;
using System.Globalization;
using System.Linq;
using System.Resources;
using System.Threading;
using System.Threading.Tasks;
using WearPartsControl.ApplicationServices.Localization.Generated;
using WearPartsControl.ApplicationServices.SaveInfoService;

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
        var config = await _saveInfoStore.ReadAsync<LocalizationOptionsSaveInfo>(cancellationToken).ConfigureAwait(false);
        await SetCultureAsync(config.CultureName, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask SetCultureAsync(string cultureName, CancellationToken cancellationToken = default)
    {
        if (!SupportedCultures.Contains(cultureName, StringComparer.OrdinalIgnoreCase))
        {
            cultureName = "zh-CN";
        }

        var culture = CultureInfo.GetCultureInfo(cultureName);
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        LocalizationBindingSource.Instance.Refresh();

        var config = new LocalizationOptionsSaveInfo { CultureName = culture.Name };
        await _saveInfoStore.WriteAsync(config, cancellationToken).ConfigureAwait(false);
    }

    private static string GetString(string name)
    {
        return ResourceManager.GetString(name, CultureInfo.CurrentUICulture) ?? name;
    }
}
