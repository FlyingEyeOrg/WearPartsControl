using WearPartsControl.ApplicationServices.ClientAppInfo;

namespace WearPartsControl.ApplicationServices.LegacyImport;

public sealed class LegacyConfigurationImportResult
{
    public string LegacyDatabasePath { get; set; } = string.Empty;

    public string LegacyRootDirectory { get; set; } = string.Empty;

    public string ResourceNumber { get; set; } = string.Empty;

    public ClientAppInfoModel ClientAppInfo { get; set; } = new();

    public bool ImportedAppSettings { get; set; }

    public bool ImportedSpacerValidation { get; set; }

    public bool ImportedUserConfig { get; set; }

    public bool ImportedComNotification { get; set; }

    public bool ImportedMhrConfig { get; set; }
}