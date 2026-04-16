namespace WearPartsControl.ApplicationServices.PartServices;

/// <summary>
/// 条码更换记录。
/// </summary>
public sealed class ReplaceRecordModel
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
    /// 基地。
    /// </summary>
    public string Site { get; set; } = string.Empty;

    /// <summary>
    /// 易损件名称。
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 旧条码。
    /// </summary>
    public string? OldNo { get; set; }

    /// <summary>
    /// 新条码。
    /// </summary>
    public string NewNo { get; set; } = string.Empty;

    /// <summary>
    /// 当前值。
    /// </summary>
    public string CurrentValue { get; set; } = string.Empty;

    /// <summary>
    /// 预警值。
    /// </summary>
    public string WarnValue { get; set; } = string.Empty;

    /// <summary>
    /// 停机值。
    /// </summary>
    public string ShutdownValue { get; set; } = string.Empty;

    /// <summary>
    /// 操作工号。
    /// </summary>
    public string OperatorNo { get; set; } = string.Empty;

    /// <summary>
    /// 操作人名称。
    /// </summary>
    public string OperatorUser { get; set; } = string.Empty;

    /// <summary>
    /// 更换说明。
    /// </summary>
    public string ReplaceMessage { get; set; } = string.Empty;

    /// <summary>
    /// 操作时间。
    /// </summary>
    public DateTime DateTime { get; set; } = DateTime.Now;

    /// <summary>
    /// 数据类型。
    /// </summary>
    public EDataType? DataType { get; set; }

    /// <summary>
    /// 业务扩展数据。
    /// </summary>
    public string? DataValue { get; set; }
}
