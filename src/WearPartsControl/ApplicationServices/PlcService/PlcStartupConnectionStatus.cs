namespace WearPartsControl.ApplicationServices.PlcService;

public enum PlcStartupConnectionStatus
{
    Uninitialized,
    Connecting,
    NotConfigured,
    Connected,
    Failed
}