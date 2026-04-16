namespace WearPartsControl.ApplicationServices.PartServices;

/// <summary>
/// 资源号对应的用户信息缓存。
/// </summary>
public sealed class UserInfoByResourceId
{
    /// <summary>
    /// 主键。
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// 资源号。
    /// </summary>
    public string ResourceId { get; set; } = string.Empty;

    /// <summary>
    /// MHR 返回结果。
    /// </summary>
    public HMRResult MhrResult { get; set; } = new();

    /// <summary>
    /// 最后更新时间。
    /// </summary>
    public DateTime LastUpdateDate { get; set; } = DateTime.Now;
}
