using WearPartsControl.ApplicationServices.LoginService;
using System.Text.Json.Serialization;

namespace WearPartsControl.ApplicationServices.PartServices;

/// <summary>
/// MHR 数据体。
/// </summary>
public sealed class MhrData
{
    /// <summary>
    /// 时间戳。
    /// </summary>
    [JsonPropertyName("timestamp")]
    public long Timestamp { get; set; }

    /// <summary>
    /// 用户列表。
    /// </summary>
    [JsonPropertyName("list")]
    public List<MhrUser> Users { get; set; } = new();

    /// <summary>
    /// 设备资源号。
    /// </summary>
    [JsonPropertyName("device_resource_id")]
    public string DeviceResourceId { get; set; } = string.Empty;
}
