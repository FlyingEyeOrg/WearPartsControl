using WearPartsControl.ApplicationServices.PlcService;
using WearPartsControl.Domain.Entities;
using WearPartsControl.Exceptions;

namespace WearPartsControl.ApplicationServices.PartServices;

internal static class WearPartPlcAccessor
{
    private const string SkipAddress = "######";

    public static PlcConnectionOptions BuildConnectionOptions(ClientAppConfigurationEntity configuration)
    {
        return PlcConnectionOptionsFactory.Create(configuration);
    }

    public static string ReadAsString(IPlcService plcService, string address, string dataType)
    {
        if (ShouldSkip(address))
        {
            return string.Empty;
        }

        return NormalizeDataType(dataType) switch
        {
            "INT" or "INT32" => plcService.Read<int>(address).ToString(),
            "FLOAT" or "SINGLE" => plcService.Read<float>(address).ToString(System.Globalization.CultureInfo.InvariantCulture),
            "DOUBLE" or "DECIMAL" => plcService.Read<double>(address).ToString(System.Globalization.CultureInfo.InvariantCulture),
            "BOOL" or "BOOLEAN" => plcService.Read<bool>(address) ? "1" : "0",
            _ => plcService.Read<string>(address)
        };
    }

    public static double ReadAsDouble(IPlcService plcService, string address, string dataType)
    {
        if (ShouldSkip(address))
        {
            return 0d;
        }

        return NormalizeDataType(dataType) switch
        {
            "INT" or "INT32" => plcService.Read<int>(address),
            "FLOAT" or "SINGLE" => plcService.Read<float>(address),
            "DOUBLE" or "DECIMAL" => plcService.Read<double>(address),
            "BOOL" or "BOOLEAN" => plcService.Read<bool>(address) ? 1d : 0d,
            _ => ParseDouble(plcService.Read<string>(address), address)
        };
    }

    public static void ClearCounter(IPlcService plcService, string address)
    {
        if (ShouldSkip(address))
        {
            return;
        }

        plcService.Write(address, 0);
    }

    public static void WriteBarcode(IPlcService plcService, string address, string barcode)
    {
        if (ShouldSkip(address))
        {
            return;
        }

        plcService.Write(address, barcode);
    }

    public static void WriteShutdownSignal(IPlcService plcService, string address, bool shutdown)
    {
        if (ShouldSkip(address))
        {
            return;
        }

        var trimmed = address.Trim();
        var invert = trimmed.StartsWith('!');
        var actualAddress = invert ? trimmed[1..] : trimmed;
        plcService.Write(actualAddress, invert ? !shutdown : shutdown);
    }

    public static PartDataType? ResolvePartDataType(string? dataType)
    {
        return NormalizeDataType(dataType) switch
        {
            "JSON" => PartDataType.Json,
            "STRING" => PartDataType.String,
            "INT" or "INT32" => PartDataType.Int,
            "FLOAT" or "SINGLE" => PartDataType.Float,
            "DOUBLE" or "DECIMAL" => PartDataType.Double,
            _ => null
        };
    }

    private static bool ShouldSkip(string address)
    {
        return string.IsNullOrWhiteSpace(address) || string.Equals(address.Trim(), SkipAddress, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeDataType(string? dataType)
    {
        return string.IsNullOrWhiteSpace(dataType) ? string.Empty : dataType.Trim().ToUpperInvariant();
    }

    private static double ParseDouble(string rawValue, string address)
    {
        if (double.TryParse(rawValue, System.Globalization.NumberStyles.Float | System.Globalization.NumberStyles.AllowThousands, System.Globalization.CultureInfo.InvariantCulture, out var value))
        {
            return value;
        }

        if (double.TryParse(rawValue, out value))
        {
            return value;
        }

        throw new UserFriendlyException($"PLC 地址 {address} 的值无法转换为数值：{rawValue}。");
    }
}