namespace WearPartsControl.ApplicationServices.PlcService;

public enum PlcStartupConnectionStatus
{
    Uninitialized,
    Connecting,
    NotConfigured,
    Connected,
    Failed
}

public sealed record PlcStartupConnectionResult(
    PlcStartupConnectionStatus Status,
    string Message)
{
    public static PlcStartupConnectionResult Uninitialized(string message = "未初始化") =>
        new(PlcStartupConnectionStatus.Uninitialized, message);

    public static PlcStartupConnectionResult Connecting(string message = "连接中") =>
        new(PlcStartupConnectionStatus.Connecting, message);

    public static PlcStartupConnectionResult NotConfigured(string message = "未配置 ClientApp，未连接 PLC。") =>
        new(PlcStartupConnectionStatus.NotConfigured, message);

    public static PlcStartupConnectionResult Connected(string message = "已连接") =>
        new(PlcStartupConnectionStatus.Connected, message);

    public static PlcStartupConnectionResult Failed(string message) =>
        new(PlcStartupConnectionStatus.Failed, message);
}