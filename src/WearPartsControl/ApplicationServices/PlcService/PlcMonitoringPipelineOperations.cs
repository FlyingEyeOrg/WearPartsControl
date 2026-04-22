namespace WearPartsControl.ApplicationServices.PlcService;

public static class PlcMonitoringPipelineOperations
{
    public const string Connect = "Monitor/Connect";
    public const string ReadCurrentValue = "Monitor/ReadCurrentValue";
    public const string ReadWarningValue = "Monitor/ReadWarningValue";
    public const string ReadShutdownValue = "Monitor/ReadShutdownValue";
    public const string WriteShutdownSignal = "Monitor/WriteShutdownSignal";
}