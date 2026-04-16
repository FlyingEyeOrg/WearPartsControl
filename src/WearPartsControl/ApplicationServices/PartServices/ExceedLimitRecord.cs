namespace WearPartsControl.ApplicationServices.PartServices;

/// <summary>
/// 超限报警记录。
/// </summary>
public sealed class ExceedLimitRecord
{
    /// <summary>
    /// 主键。
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// 易损件名称。
    /// </summary>
    public string PartName { get; set; } = string.Empty;

    /// <summary>
    /// 易损件定义主键。
    /// </summary>
    public Guid WearPartDefinitionId { get; set; }

    /// <summary>
    /// 当前值。
    /// </summary>
    public double CurrentValue { get; set; }

    /// <summary>
    /// 停机值。
    /// </summary>
    public double ShutdownValue { get; set; }

    /// <summary>
    /// 级别。
    /// </summary>
    public string Severity { get; set; } = string.Empty;

    /// <summary>
    /// 报警时间。
    /// </summary>
    public DateTime OccurredAt { get; set; } = DateTime.Now;

    /// <summary>
    /// 对应的客户端配置主键。
    /// </summary>
    public Guid ClientAppConfigurationId { get; set; }

    /// <summary>
    /// 通知消息。
    /// </summary>
    public string NotificationMessage { get; set; } = string.Empty;
}
