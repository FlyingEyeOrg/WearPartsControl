using WearPartsControl.ApplicationServices.ClientAppInfo;
using WearPartsControl.ApplicationServices.Localization;
using WearPartsControl.Domain.Entities;
using WearPartsControl.Exceptions;

namespace WearPartsControl.ApplicationServices.PlcService;

public static class PlcConnectionOptionsFactory
{
    public static PlcConnectionOptions Create(ClientAppConfigurationEntity configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return Create(
            configuration.PlcProtocolType,
            configuration.PlcIpAddress,
            configuration.PlcPort,
            configuration.SiemensRack,
            configuration.SiemensSlot,
            configuration.IsStringReverse);
    }

    public static PlcConnectionOptions Create(ClientAppInfoModel configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return Create(
            configuration.PlcProtocolType,
            configuration.PlcIpAddress,
            configuration.PlcPort,
            configuration.SiemensRack,
            configuration.SiemensSlot,
            configuration.IsStringReverse);
    }

    private static PlcConnectionOptions Create(
        string protocolType,
        string ipAddress,
        int port,
        int siemensRack,
        int siemensSlot,
        bool isStringReverse)
    {
        return new PlcConnectionOptions
        {
            PlcType = ResolveProtocolType(protocolType),
            IpAddress = ipAddress,
            Port = port,
            SiemensRack = siemensRack,
            SiemensSlot = siemensSlot,
            IsStringReverse = isStringReverse
        };
    }

    private static PlcProtocolType ResolveProtocolType(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new UserFriendlyException(LocalizedText.Get("Services.PlcConnection.ProtocolTypeRequired"));
        }

        var normalized = value.Trim().ToUpperInvariant();
        return normalized switch
        {
            "S7" or "SIEMENSS1500" => PlcProtocolType.SiemensS1500,
            "SIEMENSS1200" => PlcProtocolType.SiemensS1200,
            "MODBUSTCP" => PlcProtocolType.ModbusTcp,
            _ when Enum.TryParse<PlcProtocolType>(value, true, out var protocolType) => protocolType,
            _ => throw new UserFriendlyException(LocalizedText.Format("Services.PlcConnection.ProtocolTypeNotSupported", value))
        };
    }
}