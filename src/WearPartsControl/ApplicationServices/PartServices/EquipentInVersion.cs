namespace WearPartsControl.ApplicationServices.PartServices;

/// <summary>
/// 设备版本记录。
/// </summary>
public sealed class EquipentInVersion
{
    /// <summary>
    /// 主键。
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// 资源号。
    /// </summary>
    public string ResourceNum { get; set; } = string.Empty;

    /// <summary>
    /// 版本号。
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// 最后更新时间。
    /// </summary>
    public DateTime DateTime { get; set; } = DateTime.Now;
}
