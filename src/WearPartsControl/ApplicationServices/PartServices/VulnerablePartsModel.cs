namespace WearPartsControl.ApplicationServices.PartServices;

/// <summary>
/// 易损件定义模型。
/// </summary>
public sealed class VulnerablePartsModel
{
    /// <summary>
    /// 主键。
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// 基础配置主键。
    /// </summary>
    public string BasicModelId { get; set; } = string.Empty;

    /// <summary>
    /// 资源号。
    /// </summary>
    public string ResourceNum { get; set; } = string.Empty;

    /// <summary>
    /// 易损件名称。
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 输入方式。
    /// </summary>
    public string Input { get; set; } = string.Empty;

    /// <summary>
    /// 当前值点位。
    /// </summary>
    public string CurrentValuePoint { get; set; } = string.Empty;

    /// <summary>
    /// 当前值点位类型。
    /// </summary>
    public string CurrentValueDataType { get; set; } = string.Empty;

    /// <summary>
    /// 预警值点位。
    /// </summary>
    public string WarnValuePoint { get; set; } = string.Empty;

    /// <summary>
    /// 预警值点位类型。
    /// </summary>
    public string WarnValueDataType { get; set; } = string.Empty;

    /// <summary>
    /// 停机值点位。
    /// </summary>
    public string ShutdownValuePoint { get; set; } = string.Empty;

    /// <summary>
    /// 停机值点位类型。
    /// </summary>
    public string ShutdownValueDataType { get; set; } = string.Empty;

    /// <summary>
    /// 到期是否允许停机。
    /// </summary>
    public bool IsShutdown { get; set; }

    /// <summary>
    /// 条码最低长度。
    /// </summary>
    public int CodeMinLength { get; set; }

    /// <summary>
    /// 条码最长长度。
    /// </summary>
    public int CodeMaxLength { get; set; }

    /// <summary>
    /// 寿命类型。
    /// </summary>
    public string LifeType { get; set; } = string.Empty;

    /// <summary>
    /// PLC 清零点位。
    /// </summary>
    public string PlcZeroClear { get; set; } = string.Empty;

    /// <summary>
    /// 新条码写入 PLC 的地址。
    /// </summary>
    public string CodeWritePlcPoint { get; set; } = string.Empty;

    /// <summary>
    /// 最后更新时间。
    /// </summary>
    public DateTime? DateTime { get; set; } = System.DateTime.Now;
}
