namespace WearPartsControl.ApplicationServices.PlcService;

public interface IPlcStartupConnectionService
{
    Task<PlcStartupConnectionResult> EnsureConnectedAsync(CancellationToken cancellationToken = default);
}