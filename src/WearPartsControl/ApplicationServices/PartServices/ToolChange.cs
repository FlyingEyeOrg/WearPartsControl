namespace WearPartsControl.ApplicationServices.PartServices;

/// <summary>
/// 工具更换记录。
/// </summary>
public sealed class ToolChangeRecord
{
    /// <summary>
    /// 主键。
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// 名称。
    /// </summary>
    public string? ToolName { get; set; }

    /// <summary>
    /// 编码。
    /// </summary>
    public string? ToolCode { get; set; }

    /// <summary>
    /// 创建时间。
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
