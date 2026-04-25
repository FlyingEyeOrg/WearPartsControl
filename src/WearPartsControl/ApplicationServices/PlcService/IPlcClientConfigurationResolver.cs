namespace WearPartsControl.ApplicationServices.PlcService;

public interface IPlcClientConfigurationResolver
{
    Task<PlcClientConfigurationResolution> ResolveAsync(CancellationToken cancellationToken = default);
}