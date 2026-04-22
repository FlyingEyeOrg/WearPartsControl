namespace WearPartsControl.ApplicationServices.PlcService;

public static class PlcConfigurationPipelineOperations
{
    public const string DisconnectWhenNotConfigured = "Config/DisconnectWhenNotConfigured";
    public const string DisconnectWhenInvalid = "Config/DisconnectWhenInvalid";
    public const string ApplyAndReconnect = "Config/ApplyAndReconnect";
    public const string CheckConnectionState = "Config/CheckConnectionState";
}