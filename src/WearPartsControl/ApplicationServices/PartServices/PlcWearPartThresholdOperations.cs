namespace WearPartsControl.ApplicationServices.PartServices;

internal static class PlcWearPartThresholdOperations
{
    public const string Connect = "WearPartThreshold.Connect";
    public const string ReadWarningThreshold = "WearPartThreshold.ReadWarningThreshold";
    public const string ReadShutdownThreshold = "WearPartThreshold.ReadShutdownThreshold";
    public const string WriteWarningThreshold = "WearPartThreshold.WriteWarningThreshold";
    public const string WriteShutdownThreshold = "WearPartThreshold.WriteShutdownThreshold";
}