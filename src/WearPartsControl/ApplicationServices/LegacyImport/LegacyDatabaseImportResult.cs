using WearPartsControl.ApplicationServices.Localization;

namespace WearPartsControl.ApplicationServices.LegacyImport;

public sealed class LegacyDatabaseImportResult
{
    public string LegacyDatabasePath { get; set; } = string.Empty;

    public int ImportedClientConfigurations { get; set; }

    public int UpdatedClientConfigurations { get; set; }

    public int ImportedWearPartDefinitions { get; set; }

    public int UpdatedWearPartDefinitions { get; set; }

    public int ImportedReplacementRecords { get; set; }

    public int ImportedExceedLimitRecords { get; set; }

    public int SkippedRows { get; set; }

    public string ToSummary()
    {
        return LocalizedText.Format(
            "Services.LegacyImport.DatabaseImportSummary",
            LegacyDatabasePath,
            ImportedClientConfigurations,
            UpdatedClientConfigurations,
            ImportedWearPartDefinitions,
            UpdatedWearPartDefinitions,
            ImportedReplacementRecords,
            ImportedExceedLimitRecords,
            SkippedRows);
    }

    public string ToWearPartDefinitionSummary()
    {
        return LocalizedText.Format(
            "Services.LegacyImport.WearPartDefinitionImportSummary",
            LegacyDatabasePath,
            ImportedWearPartDefinitions,
            UpdatedWearPartDefinitions,
            SkippedRows);
    }
}