using WearPartsControl.ApplicationServices.ClientAppInfo;

namespace WearPartsControl.ApplicationServices.PlcService;

public interface IPlcConnectionTestService
{
    Task<PlcStartupConnectionResult> TestAsync(ClientAppInfoModel clientAppInfo, CancellationToken cancellationToken = default);
}