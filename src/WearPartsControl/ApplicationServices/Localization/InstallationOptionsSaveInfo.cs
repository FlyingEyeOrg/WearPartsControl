using WearPartsControl.ApplicationServices.SaveInfoService;

namespace WearPartsControl.ApplicationServices.Localization;

[SaveInfoFile("installation-options")]
public sealed class InstallationOptionsSaveInfo
{
    public string CultureName { get; set; } = string.Empty;
}
