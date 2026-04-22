namespace WearPartsControl.ApplicationServices.LegacyImport;

public interface ILegacyDatabaseImportService
{
    Task<LegacyDatabaseImportResult> ImportAsync(string legacyDatabasePath, CancellationToken cancellationToken = default);

    Task<LegacyDatabaseImportResult> ImportWearPartDefinitionsAsync(string legacyDatabasePath, Guid clientAppConfigurationId, string resourceNumber, CancellationToken cancellationToken = default);
}