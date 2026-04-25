using WearPartsControl.ApplicationServices.ClientAppInfo;

namespace WearPartsControl.ApplicationServices.PlcService;

public sealed record PlcClientConfigurationResolution(string ResourceNumber, ClientAppInfoModel? ClientAppInfo)
{
    public bool IsConfigured => ClientAppInfo is not null;

    public static PlcClientConfigurationResolution NotConfigured(string resourceNumber = "")
    {
        return new PlcClientConfigurationResolution(resourceNumber, null);
    }

    public static PlcClientConfigurationResolution Configured(string resourceNumber, ClientAppInfoModel clientAppInfo)
    {
        return new PlcClientConfigurationResolution(resourceNumber, clientAppInfo);
    }
}