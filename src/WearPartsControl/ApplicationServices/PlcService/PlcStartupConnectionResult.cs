namespace WearPartsControl.ApplicationServices.PlcService;

public enum PlcStartupConnectionStatus
{
    NotConfigured,
    Connected,
    Failed
}

public sealed record PlcStartupConnectionResult(
    PlcStartupConnectionStatus Status,
    string Message)
{
    public static PlcStartupConnectionResult NotConfigured(string message = "未配置 ClientApp，未连接 PLC。") =>
        new(PlcStartupConnectionStatus.NotConfigured, message);

    public static PlcStartupConnectionResult Connected(string message = "已连接") =>
        new(PlcStartupConnectionStatus.Connected, message);

    public static PlcStartupConnectionResult Failed(string message) =>
        new(PlcStartupConnectionStatus.Failed, message);
}