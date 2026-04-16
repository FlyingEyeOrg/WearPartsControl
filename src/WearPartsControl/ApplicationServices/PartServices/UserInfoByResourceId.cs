namespace WearPartsControl.ApplicationServices.PartServices;

/// <summary>
/// 资源号对应的用户信息缓存。
/// </summary>
public sealed class ResourceUserSnapshot
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
    /// MHR 返回结果。
    /// </summary>
    public MhrResult MhrResult { get; set; } = new();

    /// <summary>
    /// 最后更新时间。
    /// </summary>
    public DateTime LastUpdateDate { get; set; } = DateTime.Now;
}
