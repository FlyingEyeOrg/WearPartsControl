using System.Globalization;
using WearPartsControl.ApplicationServices.UserConfig;
using UserConfigModel = WearPartsControl.ApplicationServices.UserConfig.UserConfig;

namespace WearPartsControl.ApplicationServices.Localization;

internal static class LocalizationCultureState
{
    private static CultureInfo _currentCulture = ResolveInitialCulture();

    internal static CultureInfo CurrentCulture => Volatile.Read(ref _currentCulture);

    internal static void SetCurrentCulture(CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);
        Volatile.Write(ref _currentCulture, Normalize(culture));
    }

    private static CultureInfo ResolveInitialCulture()
    {
        return Normalize(CultureInfo.CurrentUICulture);
    }

    private static CultureInfo Normalize(CultureInfo culture)
    {
        return culture.Name is "zh-CN" or "en-US"
            ? CultureInfo.GetCultureInfo(culture.Name)
            : CultureInfo.GetCultureInfo(UserConfigModel.DefaultLanguage);
    }
}