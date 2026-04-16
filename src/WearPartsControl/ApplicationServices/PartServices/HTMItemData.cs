namespace WearPartsControl.ApplicationServices.PartServices;

/// <summary>
/// MHR 数据体。
/// </summary>
public sealed class HTMItemData
{
    /// <summary>
    /// 时间戳。
    /// </summary>
    public long timestamp { get; set; }

    /// <summary>
    /// 用户列表。
    /// </summary>
    public List<UserModel> list { get; set; } = new();

    /// <summary>
    /// 设备资源号。
    /// </summary>
    public string device_resource_id { get; set; } = string.Empty;
}
