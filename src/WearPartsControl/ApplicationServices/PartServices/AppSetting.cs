using WearPartsControl.ApplicationServices.SaveInfoService;

namespace WearPartsControl.ApplicationServices.PartServices;

/// <summary>
/// 本地应用配置，保存当前资源号。
/// </summary>
[SaveInfoFile("settings/app-setting")]
public sealed class AppSetting
{
    /// <summary>
    /// 当前资源号。
    /// </summary>
    public string ResourceNum { get; set; } = string.Empty;
}
