using WearPartsControl.ApplicationServices.SaveInfoService;

namespace WearPartsControl.ApplicationServices.AppSettings;

/// <summary>
/// 本地应用配置，保存当前资源号。
/// </summary>
[SaveInfoFile("app-settings")]
public sealed class AppSettings
{
    /// <summary>
    /// 当前资源号。
    /// </summary>
    public string ResourceNumber { get; set; } = string.Empty;

    /// <summary>
    /// 登录时相邻按键允许的最大时间间隔，超过则判定为手工输入。
    /// </summary>
    public int LoginInputMaxIntervalMilliseconds { get; set; } = 80;

    /// <summary>
    /// 是否已经设置客户端信息
    /// </summary>
    public bool IsSetClientAppInfo { get; set; }
}