using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Localization;
using WearPartsControl.ApplicationServices.SaveInfoService;

namespace WearPartsControl.ApplicationServices.Localization;

public sealed class LocalizationService : ILocalizationService
{
    private static readonly string[] SupportedCultures = ["zh-CN", "en-US"];

    private readonly IStringLocalizer<LocalizationResource> _localizer;
    private readonly ISaveInfoStore _saveInfoStore;

    public LocalizationService(IStringLocalizer<LocalizationResource> localizer, ISaveInfoStore saveInfoStore)
    {
        _localizer = localizer;
        _saveInfoStore = saveInfoStore;
    }

    public string this[string name] => _localizer[name];

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

        var config = new LocalizationOptionsSaveInfo { CultureName = culture.Name };
        await _saveInfoStore.WriteAsync(config, cancellationToken).ConfigureAwait(false);
    }
}
