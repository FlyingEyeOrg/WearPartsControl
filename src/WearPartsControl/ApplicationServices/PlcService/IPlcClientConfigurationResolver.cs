namespace WearPartsControl.ApplicationServices.PlcService;

public interface IPlcClientConfigurationResolver
{
    Task<PlcClientConfigurationResult> ResolveAsync(CancellationToken cancellationToken = default);
}