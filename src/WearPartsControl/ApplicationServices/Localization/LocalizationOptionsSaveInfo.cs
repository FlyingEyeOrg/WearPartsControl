using WearPartsControl.ApplicationServices.SaveInfoService;

namespace WearPartsControl.ApplicationServices.Localization;

[SaveInfoFile("settings/localization-options")]
public sealed class LocalizationOptionsSaveInfo
{
    public string CultureName { get; set; } = "zh-CN";
}
