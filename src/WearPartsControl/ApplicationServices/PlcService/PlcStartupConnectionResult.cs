using WearPartsControl.ApplicationServices.Localization;

namespace WearPartsControl.ApplicationServices.PlcService;

public sealed record PlcStartupConnectionResult(
    PlcStartupConnectionStatus Status,
    string Message)
{
    public static PlcStartupConnectionResult Uninitialized(string? message = null) =>
        new(PlcStartupConnectionStatus.Uninitialized, message ?? LocalizedText.Get("Services.PlcStartupConnection.Uninitialized"));

    public static PlcStartupConnectionResult Connecting(string? message = null) =>
        new(PlcStartupConnectionStatus.Connecting, message ?? LocalizedText.Get("Services.PlcStartupConnection.Connecting"));

    public static PlcStartupConnectionResult NotConfigured(string? message = null) =>
        new(PlcStartupConnectionStatus.NotConfigured, message ?? LocalizedText.Get("Services.PlcStartupConnection.NotConfigured"));

    public static PlcStartupConnectionResult Connected(string? message = null) =>
        new(PlcStartupConnectionStatus.Connected, message ?? LocalizedText.Get("Services.PlcStartupConnection.Connected"));

    public static PlcStartupConnectionResult Failed(string message) =>
        new(PlcStartupConnectionStatus.Failed, message);
}