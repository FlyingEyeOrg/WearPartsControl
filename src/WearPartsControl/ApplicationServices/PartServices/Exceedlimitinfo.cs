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
    /// 当前值。
    /// </summary>
    public double CurrentValue { get; set; }

    /// <summary>
    /// 停机值。
    /// </summary>
    public double ShutdownValue { get; set; }

    /// <summary>
    /// 报警时间。
    /// </summary>
    public DateTime OccurredAt { get; set; } = DateTime.Now;

    /// <summary>
    /// 对应的基础配置主键。
    /// </summary>
    public Guid BasicConfigurationId { get; set; }
}
