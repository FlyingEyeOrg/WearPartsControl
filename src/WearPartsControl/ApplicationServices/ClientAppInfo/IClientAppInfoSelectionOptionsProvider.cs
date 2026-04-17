namespace WearPartsControl.ApplicationServices.ClientAppInfo;

public interface IClientAppInfoSelectionOptionsProvider
{
    Task<ClientAppInfoSelectionOptions> GetAsync(CancellationToken cancellationToken = default);
}
