namespace WearPartsControl.ApplicationServices.ClientAppInfo;

public interface IClientAppInfoSelectionOptionsProvider
{
    Task<ClientAppInfoSelectionOptions> GetAsync(CancellationToken cancellationToken = default);

    Task<string> MapAreaOptionAsync(string value, string targetCultureName, CancellationToken cancellationToken = default);

    Task<string> MapProcedureOptionAsync(string value, string targetCultureName, CancellationToken cancellationToken = default);
}
