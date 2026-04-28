using System;
using System.Globalization;
using System.Linq;
using System.Resources;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
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
    private readonly IUserConfigService _userConfigService;
    private readonly LocalizationCatalog _catalog;
    private CultureInfo _currentCulture;

    public LocalizationService(ISaveInfoStore saveInfoStore, IUserConfigService userConfigService)
    {
        _saveInfoStore = saveInfoStore;
        _userConfigService = userConfigService;
        _currentCulture = LocalizationCultureState.CurrentCulture;
        _catalog = new LocalizationCatalog(GetString);
    }

    public string this[string name] => GetString(name);

    public LocalizationCatalog Catalog => _catalog;

    public CultureInfo CurrentCulture => Volatile.Read(ref _currentCulture);

    public async ValueTask InitializeAsync(CancellationToken cancellationToken = default)
    {
        var userConfig = await _userConfigService.GetAsync(cancellationToken).ConfigureAwait(false);
        var cultureName = string.IsNullOrWhiteSpace(userConfig.Language)
            ? await ResolveFirstRunCultureNameAsync(cancellationToken).ConfigureAwait(false)
            : userConfig.Language;
        await SetCultureAsync(cultureName, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask SetCultureAsync(string cultureName, CancellationToken cancellationToken = default)
    {
        cultureName = NormalizeCultureName(cultureName);

        var culture = CultureInfo.GetCultureInfo(cultureName);
        Volatile.Write(ref _currentCulture, culture);
        LocalizedText.SetCulture(culture);
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        ApplyCultureOnUiThread(culture);

        var userConfig = await _saveInfoStore.ReadAsync<UserConfigModel>(cancellationToken).ConfigureAwait(false);
        if (!string.Equals(userConfig.Language, culture.Name, StringComparison.Ordinal))
        {
            userConfig.Language = culture.Name;
            await _saveInfoStore.WriteAsync(userConfig, cancellationToken).ConfigureAwait(false);
        }

        if (_saveInfoStore is TypeJsonSaveInfoStore fileStore && fileStore.Exists<LocalizationOptionsSaveInfo>())
        {
            await fileStore.DeleteAsync<LocalizationOptionsSaveInfo>(cancellationToken).ConfigureAwait(false);
        }
    }

    private static string NormalizeCultureName(string? cultureName)
    {
        return SupportedCultures.Contains(cultureName ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            ? CultureInfo.GetCultureInfo(cultureName!).Name
                : UserConfigModel.DefaultLanguage;
    }

    private async ValueTask<string> ResolveFirstRunCultureNameAsync(CancellationToken cancellationToken)
    {
        var installationOptions = await _saveInfoStore.ReadAsync<InstallationOptionsSaveInfo>(cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(installationOptions.CultureName))
        {
            return installationOptions.CultureName;
        }

        return ResolveSystemCultureName(CultureInfo.CurrentUICulture);
    }

    private static string ResolveSystemCultureName(CultureInfo systemCulture)
    {
        return SupportedCultures.Contains(systemCulture.Name, StringComparer.OrdinalIgnoreCase)
            ? CultureInfo.GetCultureInfo(systemCulture.Name).Name
            : UserConfigModel.DefaultLanguage;
    }

    private string GetString(string name)
    {
        return ResourceManager.GetString(name, CurrentCulture) ?? name;
    }

    private static void ApplyCultureOnUiThread(CultureInfo culture)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
            LocalizationBindingSource.Instance.Refresh();
            return;
        }

        if (dispatcher.CheckAccess())
        {
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
            LocalizationBindingSource.Instance.Refresh();
            return;
        }

        dispatcher.Invoke(() =>
        {
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
            LocalizationBindingSource.Instance.Refresh();
        });
    }
}
