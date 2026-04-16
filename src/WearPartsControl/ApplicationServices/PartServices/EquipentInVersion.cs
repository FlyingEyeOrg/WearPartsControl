namespace WearPartsControl.ApplicationServices.PartServices;

/// <summary>
/// 设备版本记录。
/// </summary>
public sealed class EquipmentVersionRecord
{
    /// <summary>
    /// 主键。
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// 资源号。
    /// </summary>
    public string ResourceNumber { get; set; } = string.Empty;

    /// <summary>
    /// 版本号。
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// 最后更新时间。
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}
