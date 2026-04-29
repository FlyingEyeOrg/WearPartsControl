using WearPartsControl.ApplicationServices.PartServices;
using WearPartsControl.ApplicationServices.PlcService;

namespace WearPartsControl.ApplicationServices.LegacyImport;

internal static class LegacyImportValueConverter
{
    private static readonly IReadOnlyDictionary<string, string> PlcProtocolAliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["S7"] = nameof(PlcProtocolType.SiemensS1500),
        ["SIEMENSS7"] = nameof(PlcProtocolType.SiemensS1500),
        ["西门子S7"] = nameof(PlcProtocolType.SiemensS1500),
        ["西门子S1500"] = nameof(PlcProtocolType.SiemensS1500),
        ["西门子S1200"] = nameof(PlcProtocolType.SiemensS1200),
        ["欧姆龙CIP"] = nameof(PlcProtocolType.OmronCip),
        ["欧姆龙FINS"] = nameof(PlcProtocolType.OmronFins),
        ["三菱"] = nameof(PlcProtocolType.Mitsubishi),
        ["罗克韦尔"] = nameof(PlcProtocolType.AllenBradley),
        ["汇川AM"] = nameof(PlcProtocolType.InovanceAm),
        ["汇川H3U"] = nameof(PlcProtocolType.InovanceH3U),
        ["汇川H5U"] = nameof(PlcProtocolType.InovanceH5U),
        ["汇川EIP"] = nameof(PlcProtocolType.InovanceEip),
        ["倍福"] = nameof(PlcProtocolType.Beckhoff),
        ["基恩士"] = nameof(PlcProtocolType.Keyence),
        ["MODBUSTCP"] = nameof(PlcProtocolType.ModbusTcp)
    };

    private static readonly IReadOnlyDictionary<string, string> InputModeAliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["MANUAL"] = "Manual",
        ["键盘"] = "Manual",
        ["手动"] = "Manual",
        ["SCANNER"] = "Scanner",
        ["BARCODE"] = "Scanner",
        ["扫码"] = "Scanner",
        ["扫码枪"] = "Scanner"
    };

    private static readonly IReadOnlyDictionary<string, string> LifetimeTypeAliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["METER"] = "记米",
        ["记米"] = "记米",
        ["计米"] = "记米",
        ["COUNT"] = "计次",
        ["计次"] = "计次",
        ["TIME"] = "计时",
        ["计时"] = "计时",
        ["时间"] = "计时"
    };

    private static readonly IReadOnlyDictionary<string, string> ReplacementDataTypeAliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["0"] = WearPartPlcDataTypes.Json,
        ["JSON"] = WearPartPlcDataTypes.Json,
        ["1"] = WearPartPlcDataTypes.String,
        ["STRING"] = WearPartPlcDataTypes.String,
        ["STR"] = WearPartPlcDataTypes.String,
        ["2"] = WearPartPlcDataTypes.Int32,
        ["INT"] = WearPartPlcDataTypes.Int32,
        ["DINT"] = WearPartPlcDataTypes.Int32,
        ["INT32"] = WearPartPlcDataTypes.Int32,
        ["INT16"] = WearPartPlcDataTypes.Int16,
        ["SHORT"] = WearPartPlcDataTypes.Int16,
        ["UDINT"] = WearPartPlcDataTypes.UInt32,
        ["UINT"] = WearPartPlcDataTypes.UInt32,
        ["UINT32"] = WearPartPlcDataTypes.UInt32,
        ["3"] = WearPartPlcDataTypes.Float,
        ["REAL"] = WearPartPlcDataTypes.Float,
        ["FLOAT"] = WearPartPlcDataTypes.Float,
        ["SINGLE"] = WearPartPlcDataTypes.Float,
        ["4"] = WearPartPlcDataTypes.Double,
        ["LREAL"] = WearPartPlcDataTypes.Double,
        ["DOUBLE"] = WearPartPlcDataTypes.Double,
        ["DECIMAL"] = WearPartPlcDataTypes.Double,
        ["BOOL"] = WearPartPlcDataTypes.Bool,
        ["BOOLEAN"] = WearPartPlcDataTypes.Bool
    };

    public static string NormalizePlcProtocolType(string? value, string fallback = nameof(PlcProtocolType.SiemensS1500))
    {
        var normalized = NormalizeOrEmpty(value);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return fallback;
        }

        if (PlcProtocolAliases.TryGetValue(NormalizeKey(normalized), out var alias))
        {
            return alias;
        }

        return Enum.TryParse<PlcProtocolType>(normalized, true, out var parsed)
            ? parsed.ToString()
            : normalized;
    }

    public static string NormalizeWearPartDataType(string? value, string fallback = WearPartPlcDataTypes.String)
    {
        return WearPartPlcDataTypes.Normalize(value, fallback);
    }

    public static string? NormalizeReplacementDataType(string? value)
    {
        var normalized = NormalizeOrEmpty(value);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        var key = NormalizeKey(normalized);
        return ReplacementDataTypeAliases.TryGetValue(key, out var alias)
            ? alias
            : WearPartPlcDataTypes.Normalize(normalized);
    }

    public static string NormalizeInputMode(string? value)
    {
        var normalized = NormalizeOrEmpty(value, "Scanner");
        return InputModeAliases.TryGetValue(NormalizeKey(normalized), out var alias)
            ? alias
            : normalized;
    }

    public static string NormalizeLifetimeType(string? value)
    {
        var normalized = NormalizeOrEmpty(value, "计次");
        return LifetimeTypeAliases.TryGetValue(NormalizeKey(normalized), out var alias)
            ? alias
            : normalized;
    }

    public static string NormalizeReplacementReason(string? value)
    {
        return WearPartReplacementReason.NormalizeCode(NormalizeOrEmpty(value));
    }

    private static string NormalizeOrEmpty(string? value, string fallback = "")
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    private static string NormalizeKey(string value)
    {
        return value.Replace("_", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .ToUpperInvariant();
    }
}
