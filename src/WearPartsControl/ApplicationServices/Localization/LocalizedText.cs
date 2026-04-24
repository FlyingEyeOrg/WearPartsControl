using System.Globalization;
using System.Resources;

namespace WearPartsControl.ApplicationServices.Localization;

public static class LocalizedText
{
    private static readonly ResourceManager ResourceManager = new("WearPartsControl.Resources.LocalizationResource", typeof(LocalizedText).Assembly);

    internal static CultureInfo CurrentCulture => LocalizationCultureState.CurrentCulture;

    internal static void SetCulture(CultureInfo culture)
    {
        LocalizationCultureState.SetCurrentCulture(culture);
    }

    public static string Get(string key)
    {
        var culture = CurrentCulture;
        return ResourceManager.GetString(key, culture)
            ?? ResourceManager.GetString(key, CultureInfo.GetCultureInfo("zh-CN"))
            ?? key;
    }

    public static string Format(string key, params object[] args)
    {
        return string.Format(CurrentCulture, Get(key), args);
    }
}