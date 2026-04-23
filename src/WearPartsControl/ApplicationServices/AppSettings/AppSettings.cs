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
    /// 自动注销倒计时，单位秒。
    /// </summary>
    public int AutoLogoutCountdownSeconds { get; set; } = 360;

    /// <summary>
    /// 是否启用工号登录。默认为刷卡登录。
    /// </summary>
    public bool UseWorkNumberLogin { get; set; }

    /// <summary>
    /// PLC 管线监控配置。
    /// </summary>
    public PlcPipelineSettings PlcPipeline { get; set; } = new();

    /// <summary>
    /// 是否已经设置客户端信息
    /// </summary>
    public bool IsSetClientAppInfo { get; set; }

    /// <summary>
    /// 是否启用易损件后台监控。
    /// </summary>
    public bool IsWearPartMonitoringEnabled { get; set; }
}

public sealed class PlcPipelineSettings
{
    /// <summary>
    /// PLC 管线排队等待慢调用阈值，单位毫秒。
    /// </summary>
    public int SlowQueueWaitThresholdMilliseconds { get; set; } = 100;

    /// <summary>
    /// PLC 管线执行慢调用阈值，单位毫秒。
    /// </summary>
    public int SlowExecutionThresholdMilliseconds { get; set; } = 500;
}