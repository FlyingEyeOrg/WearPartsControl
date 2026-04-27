using WearPartsControl.ApplicationServices.SaveInfoService;

namespace WearPartsControl.ApplicationServices.PartServices;

[SaveInfoFile("wear-part-alert-popup-state")]
public sealed class WearPartAlertPopupSaveInfo
{
    public string LastShownLocalDate { get; set; } = string.Empty;
}