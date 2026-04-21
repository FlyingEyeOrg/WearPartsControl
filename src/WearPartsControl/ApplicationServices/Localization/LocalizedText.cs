using System.Globalization;
using System.Resources;

namespace WearPartsControl.ApplicationServices.Localization;

public static class LocalizedText
{
    private static readonly ResourceManager ResourceManager = new("WearPartsControl.Resources.LocalizationResource", typeof(LocalizedText).Assembly);

    public static string Get(string key)
    {
        var culture = ResolveCulture();
        return ResourceManager.GetString(key, culture)
            ?? ResourceManager.GetString(key, CultureInfo.GetCultureInfo("zh-CN"))
            ?? key;
    }

    public static string Format(string key, params object[] args)
    {
        return string.Format(ResolveCulture(), Get(key), args);
    }

    private static CultureInfo ResolveCulture()
    {
        var culture = CultureInfo.CurrentUICulture;
        return culture.Name is "zh-CN" or "en-US"
            ? culture
            : CultureInfo.GetCultureInfo("zh-CN");
    }
}