using WearPartsControl.ApplicationServices.Localization;
using WearPartsControl.Exceptions;

namespace WearPartsControl.ApplicationServices.PartServices;

internal static class WearPartReplacementValueParser
{
    public static double ParseDouble(string rawValue, string dataType, string address)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return 0d;
        }

        if (double.TryParse(rawValue, System.Globalization.NumberStyles.Float | System.Globalization.NumberStyles.AllowThousands, System.Globalization.CultureInfo.InvariantCulture, out var invariantValue))
        {
            return invariantValue;
        }

        if (double.TryParse(rawValue, out var localValue))
        {
            return localValue;
        }

        throw new UserFriendlyException(LocalizedText.Format("Services.WearPartReplacement.ValueCannotConvert", address, rawValue), code: "WearPartReplacement:ValueInvalid");
    }
}