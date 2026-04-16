namespace WearPartsControl.ApplicationServices.PartServices;

/// <summary>
/// 条码更换记录。
/// </summary>
public sealed class WearPartReplacementRecord
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
    /// 易损件定义主键。
    /// </summary>
    public Guid WearPartDefinitionId { get; set; }

    /// <summary>
    /// 基地。
    /// </summary>
    public string SiteCode { get; set; } = string.Empty;

    /// <summary>
    /// 易损件名称。
    /// </summary>
    public string PartName { get; set; } = string.Empty;

    /// <summary>
    /// 旧条码。
    /// </summary>
    public string? OldBarcode { get; set; }

    /// <summary>
    /// 新条码。
    /// </summary>
    public string NewBarcode { get; set; } = string.Empty;

    /// <summary>
    /// 当前值。
    /// </summary>
    public string CurrentValue { get; set; } = string.Empty;

    /// <summary>
    /// 预警值。
    /// </summary>
    public string WarningValue { get; set; } = string.Empty;

    /// <summary>
    /// 停机值。
    /// </summary>
    public string ShutdownValue { get; set; } = string.Empty;

    /// <summary>
    /// 操作工号。
    /// </summary>
    public string OperatorWorkNumber { get; set; } = string.Empty;

    /// <summary>
    /// 操作人名称。
    /// </summary>
    public string OperatorUserName { get; set; } = string.Empty;

    /// <summary>
    /// 更换说明。
    /// </summary>
    public string ReplacementMessage { get; set; } = string.Empty;

    /// <summary>
    /// 更换原因。
    /// </summary>
    public string ReplacementReason { get; set; } = string.Empty;

    /// <summary>
    /// 操作时间。
    /// </summary>
    public DateTime ReplacedAt { get; set; } = DateTime.Now;

    /// <summary>
    /// 数据类型。
    /// </summary>
    public PartDataType? DataType { get; set; }

    /// <summary>
    /// 业务扩展数据。
    /// </summary>
    public string? DataValue { get; set; }
}
