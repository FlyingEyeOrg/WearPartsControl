namespace WearPartsControl.ApplicationServices.ClientAppInfo;

public interface IClientAppInfoService
{
    Task<ClientAppInfoModel> GetAsync(CancellationToken cancellationToken = default);

    Task<ClientAppInfoModel> SaveAsync(ClientAppInfoSaveRequest request, CancellationToken cancellationToken = default);
}