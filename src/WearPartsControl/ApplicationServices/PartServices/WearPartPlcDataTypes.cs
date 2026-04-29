namespace WearPartsControl.ApplicationServices.PartServices;

internal static class WearPartPlcDataTypes
{
    public const string Json = "JSON";
    public const string String = "STRING";
    public const string Int16 = "INT16";
    public const string Int32 = "INT32";
    public const string UInt32 = "UINT32";
    public const string Float = "FLOAT";
    public const string Double = "DOUBLE";
    public const string Bool = "BOOL";

    public static IReadOnlyList<string> EditorOptions { get; } =
    [
        Float,
        Double,
        Int16,
        Int32,
        UInt32,
        Bool,
        String,
        Json
    ];

    public static string Normalize(string? dataType, string fallback = String)
    {
        if (string.IsNullOrWhiteSpace(dataType))
        {
            return fallback;
        }

        var trimmed = dataType.Trim();
        return NormalizeKey(trimmed) switch
        {
            "JSON" => Json,
            "STRING" or "STR" => String,
            "INT" or "INT16" or "SHORT" => Int16,
            "DINT" or "INT32" or "INTEGER" => Int32,
            "UDINT" or "UINT" or "UINT32" or "DWORD" => UInt32,
            "REAL" or "FLOAT" or "SINGLE" => Float,
            "LREAL" or "DOUBLE" or "DECIMAL" => Double,
            "BOOL" or "BOOLEAN" => Bool,
            _ => trimmed.ToUpperInvariant()
        };
    }

    private static string NormalizeKey(string value)
    {
        return value.Replace("_", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .ToUpperInvariant();
    }
}
