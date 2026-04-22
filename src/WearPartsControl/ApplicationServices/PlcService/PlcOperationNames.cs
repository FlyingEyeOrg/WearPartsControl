namespace WearPartsControl.ApplicationServices.PlcService;

public static class PlcOperationNames
{
    public static class Startup
    {
        public const string EnsureConnected = "Startup/EnsureConnected";
    }

    public static class Configuration
    {
        public const string DisconnectWhenNotConfigured = "Config/DisconnectWhenNotConfigured";
        public const string DisconnectWhenInvalid = "Config/DisconnectWhenInvalid";
        public const string ApplyAndReconnect = "Config/ApplyAndReconnect";
        public const string CheckConnectionState = "Config/CheckConnectionState";
    }

    public static class Monitor
    {
        public const string Connect = "Monitor/Connect";
        public const string ReadCurrentValue = "Monitor/ReadCurrentValue";
        public const string ReadWarningValue = "Monitor/ReadWarningValue";
        public const string ReadShutdownValue = "Monitor/ReadShutdownValue";
        public const string WriteShutdownSignal = "Monitor/WriteShutdownSignal";
    }

    public static class Replacement
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
}