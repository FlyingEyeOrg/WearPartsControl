namespace WearPartsControl.ApplicationServices.PartServices;

/// <summary>
/// 超限报警记录。
/// </summary>
public sealed class Exceedlimitinfo
{
    /// <summary>
    /// 主键。
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// 易损件名称。
    /// </summary>
    public string Name { get; set; } = string.Empty;

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
    public DateTime DateTime { get; set; } = DateTime.Now;

    /// <summary>
    /// 对应的基础配置主键。
    /// </summary>
    public string BasicId { get; set; } = string.Empty;
}
