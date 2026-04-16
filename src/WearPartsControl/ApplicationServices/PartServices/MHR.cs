using WearPartsControl.ApplicationServices.SaveInfoService;

namespace WearPartsControl.ApplicationServices.PartServices;

/// <summary>
/// MHR 接口配置。
/// </summary>
[SaveInfoFile("settings/mhr")]
public sealed class MHR
{
    /// <summary>
    /// 获取 token 的地址。
    /// </summary>
    public string GetTokenUrl { get; set; } = string.Empty;

    /// <summary>
    /// 登录用户名。
    /// </summary>
    public string LoginName { get; set; } = string.Empty;

    /// <summary>
    /// 登录密码。
    /// </summary>
    public string LoginPassword { get; set; } = string.Empty;

    /// <summary>
    /// 获取人员列表的地址。
    /// </summary>
    public string GetListUrl { get; set; } = string.Empty;

    /// <summary>
    /// 默认更新时间。
    /// </summary>
    public int UpdateDate { get; set; }
}
