using WearPartsControl.ApplicationServices.ClientAppInfo;

namespace WearPartsControl.ApplicationServices.PlcService;

public sealed record PlcClientConfigurationResult(string ResourceNumber, ClientAppInfoModel? ClientAppInfo)
{
    public bool IsConfigured => ClientAppInfo is not null;

    public static PlcClientConfigurationResult NotConfigured(string resourceNumber = "")
    {
        return new PlcClientConfigurationResult(resourceNumber, null);
    }

    public static PlcClientConfigurationResult Configured(string resourceNumber, ClientAppInfoModel clientAppInfo)
    {
        return new PlcClientConfigurationResult(resourceNumber, clientAppInfo);
    }
}