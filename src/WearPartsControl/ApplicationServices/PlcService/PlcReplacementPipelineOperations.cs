namespace WearPartsControl.ApplicationServices.PlcService;

public static class PlcReplacementPipelineOperations
{
    public const string ConnectPreview = "Replacement/ConnectPreview";
    public const string GetPreviewCurrentValue = "Replacement/GetPreviewCurrentValue";
    public const string GetPreviewWarningValue = "Replacement/GetPreviewWarningValue";
    public const string GetPreviewShutdownValue = "Replacement/GetPreviewShutdownValue";
    public const string ConnectReplace = "Replacement/ConnectReplace";
    public const string ReadCurrentValue = "Replacement/ReadCurrentValue";
    public const string ReadWarningValue = "Replacement/ReadWarningValue";
    public const string ReadShutdownValue = "Replacement/ReadShutdownValue";
    public const string PulseZeroClear = "Replacement/PulseZeroClear";
    public const string WriteCurrentValue = "Replacement/WriteCurrentValue";
    public const string WriteBarcode = "Replacement/WriteBarcode";
    public const string WriteShutdownSignal = "Replacement/WriteShutdownSignal";
}