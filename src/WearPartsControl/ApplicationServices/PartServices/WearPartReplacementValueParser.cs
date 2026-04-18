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

        throw new UserFriendlyException($"PLC 地址 {address} 的值无法转换为数值：{rawValue}。", code: "WearPartReplacement:ValueInvalid");
    }
}