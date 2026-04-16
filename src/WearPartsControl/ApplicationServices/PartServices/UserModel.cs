using System.Text.Json.Serialization;

namespace WearPartsControl.ApplicationServices.PartServices;

/// <summary>
/// MHR 人员信息。
/// </summary>
public sealed class MhrUser
{
    /// <summary>
    /// 工号。
    /// </summary>
    [JsonPropertyName("work_id")]
    public string WorkId { get; set; } = string.Empty;

    /// <summary>
    /// 权限等级。
    /// </summary>
    [JsonPropertyName("access_level")]
    public int AccessLevel { get; set; }

    /// <summary>
    /// 卡号。
    /// </summary>
    [JsonPropertyName("card_id")]
    public string CardId { get; set; } = string.Empty;
}
