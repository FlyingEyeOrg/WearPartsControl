namespace WearPartsControl.ApplicationServices.PartServices;

/// <summary>
/// 易损件定义模型。
/// </summary>
public sealed class WearPartDefinition
{
    /// <summary>
    /// 主键。
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// 客户端配置主键。
    /// </summary>
    public Guid ClientAppConfigurationId { get; set; }

    /// <summary>
    /// 资源号。
    /// </summary>
    public string ResourceNumber { get; set; } = string.Empty;

    /// <summary>
    /// 易损件名称。
    /// </summary>
    public string PartName { get; set; } = string.Empty;

    /// <summary>
    /// 输入方式。
    /// </summary>
    public string InputMode { get; set; } = string.Empty;

    /// <summary>
    /// 当前值点位。
    /// </summary>
    public string CurrentValueAddress { get; set; } = string.Empty;

    /// <summary>
    /// 当前值点位类型。
    /// </summary>
    public string CurrentValueDataType { get; set; } = string.Empty;

    /// <summary>
    /// 预警值点位。
    /// </summary>
    public string WarningValueAddress { get; set; } = string.Empty;

    /// <summary>
    /// 预警值点位类型。
    /// </summary>
    public string WarningValueDataType { get; set; } = string.Empty;

    /// <summary>
    /// 停机值点位。
    /// </summary>
    public string ShutdownValueAddress { get; set; } = string.Empty;

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
    public string LifetimeType { get; set; } = string.Empty;

    /// <summary>
    /// 关联的换刀类型主键。
    /// </summary>
    public Guid? ToolChangeId { get; set; }

    /// <summary>
    /// PLC 清零点位。
    /// </summary>
    public string PlcZeroClearAddress { get; set; } = string.Empty;

    /// <summary>
    /// 新条码写入 PLC 的地址。
    /// </summary>
    public string BarcodeWriteAddress { get; set; } = string.Empty;

    /// <summary>
    /// 创建时间。
    /// </summary>
    public DateTime? CreatedAt { get; set; }

    /// <summary>
    /// 创建人。
    /// </summary>
    public string CreatedBy { get; set; } = string.Empty;

    /// <summary>
    /// 更新人。
    /// </summary>
    public string UpdatedBy { get; set; } = string.Empty;

    /// <summary>
    /// 最后更新时间。
    /// </summary>
    public DateTime? UpdatedAt { get; set; } = DateTime.Now;
}
