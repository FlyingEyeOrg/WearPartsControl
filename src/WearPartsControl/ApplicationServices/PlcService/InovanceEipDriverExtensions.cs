using PlcGateway.Drivers.Inovance;

namespace WearPartsControl.ApplicationServices.PlcService;

internal static class InovanceEipDriverExtensions
{
    public static string ReadByType(this InovanceEIPDriver driver, string address, PlcDataType dataType)
    {
        return dataType switch
        {
            PlcDataType.Int => PlcTypeConversion.ToInvariantString(driver.Read<short>(address)),
            PlcDataType.DInt => PlcTypeConversion.ToInvariantString(driver.Read<int>(address)),
            PlcDataType.UDInt => PlcTypeConversion.ToInvariantString(driver.Read<uint>(address)),
            PlcDataType.Bool => PlcTypeConversion.ToInvariantString(driver.Read<bool>(address)),
            PlcDataType.Real => PlcTypeConversion.ToInvariantString(driver.Read<float>(address)),
            PlcDataType.LReal => PlcTypeConversion.ToInvariantString(driver.Read<double>(address)),
            PlcDataType.String => driver.Read<string>(address),
            _ => throw new InvalidOperationException($"不支持读取的数据类型: {dataType}")
        };
    }

    public static void WriteByType(this InovanceEIPDriver driver, string address, PlcDataType dataType, string value)
    {
        switch (dataType)
        {
            case PlcDataType.Int:
                driver.Write(address, PlcTypeConversion.ParseInt16(value));
                return;
            case PlcDataType.DInt:
                driver.Write(address, PlcTypeConversion.ParseInt32(value));
                return;
            case PlcDataType.UDInt:
                driver.Write(address, PlcTypeConversion.ParseUInt32(value));
                return;
            case PlcDataType.Bool:
                driver.Write(address, PlcTypeConversion.ParseBool(value));
                return;
            case PlcDataType.Real:
                driver.Write(address, PlcTypeConversion.ParseFloat(value));
                return;
            case PlcDataType.LReal:
                driver.Write(address, PlcTypeConversion.ParseDouble(value));
                return;
            case PlcDataType.String:
                driver.Write(address, value);
                return;
            default:
                throw new InvalidOperationException($"不支持写入的数据类型: {dataType}");
        }
    }
}
