using WearPartsControl.ApplicationServices.SaveInfoService;

namespace WearPartsControl.ApplicationServices.UserConfig;

[SaveInfoFile("user-config")]
public sealed class UserConfig
{
    public string MeResponsibleWorkId { get; set; } = string.Empty;

    public string PrdResponsibleWorkId { get; set; } = string.Empty;

    public string ComAccessToken { get; set; } = string.Empty;

    public string ComSecret { get; set; } = string.Empty;
}