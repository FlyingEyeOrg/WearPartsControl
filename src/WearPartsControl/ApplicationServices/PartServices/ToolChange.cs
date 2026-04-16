namespace WearPartsControl.ApplicationServices.PartServices;

/// <summary>
/// 工具更换记录。
/// </summary>
public sealed class ToolChange
{
    /// <summary>
    /// 主键。
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// 名称。
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// 编码。
    /// </summary>
    public string? Code { get; set; }

    /// <summary>
    /// 创建时间。
    /// </summary>
    public string CreatTime { get; set; } = string.Empty;
}
