namespace WearPartsControl.ApplicationServices.ConfigurationTransfer;

public interface IConfigurationTransferService
{
    Task<ConfigurationTransferSummary> ExportAsync(string packagePath, CancellationToken cancellationToken = default);

    Task<ConfigurationTransferSummary> ImportAsync(string packagePath, CancellationToken cancellationToken = default);
}