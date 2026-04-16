namespace WearPartsControl.ApplicationServices.LegacyImport;

public interface ILegacyDatabaseImportService
{
    Task<LegacyDatabaseImportResult> ImportAsync(string legacyDatabasePath, CancellationToken cancellationToken = default);
}