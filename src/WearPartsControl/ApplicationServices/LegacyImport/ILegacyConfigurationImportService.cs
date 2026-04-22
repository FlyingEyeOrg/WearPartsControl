namespace WearPartsControl.ApplicationServices.LegacyImport;

public interface ILegacyConfigurationImportService
{
    Task<LegacyConfigurationImportResult> ImportAsync(string legacyDatabasePath, CancellationToken cancellationToken = default);
}