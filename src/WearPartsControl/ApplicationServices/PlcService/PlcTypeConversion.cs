using System.Globalization;

namespace WearPartsControl.ApplicationServices.PlcService;

internal static class PlcTypeConversion
{
    public static short ParseInt16(string value) =>
        short.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture);

    public static int ParseInt32(string value) =>
        int.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture);

    public static uint ParseUInt32(string value) =>
        uint.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture);

    public static float ParseFloat(string value) =>
        float.Parse(value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture);

    public static double ParseDouble(string value) =>
        double.Parse(value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture);

    public static bool ParseBool(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        if (bool.TryParse(value, out var boolValue))
        {
            return boolValue;
        }

        return value.Trim() switch
        {
            "1" => true,
            "0" => false,
            _ => throw new FormatException($"布尔值格式错误: {value}")
        };
    }

    public static string ToInvariantString<T>(T value)
    {
        if (value is null)
        {
            return string.Empty;
        }

        return value switch
        {
            float floatValue => floatValue.ToString(CultureInfo.InvariantCulture),
            double doubleValue => doubleValue.ToString(CultureInfo.InvariantCulture),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty
        };
    }
}
