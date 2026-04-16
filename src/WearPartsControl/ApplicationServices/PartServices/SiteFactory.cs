using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace WearPartsControl.ApplicationServices.PartServices;

/// <summary>
/// 基地与工厂的分组配置模型。
/// </summary>
public sealed class SiteFactoryMapping
{
    /// <summary>
    /// 基地编码。
    /// </summary>
    [JsonPropertyName("Site")]
    public string SiteCode { get; set; } = string.Empty;

    /// <summary>
    /// 基地名称。
    /// </summary>
    [JsonPropertyName("SiteName")]
    public string SiteName { get; set; } = string.Empty;

    /// <summary>
    /// 该基地下的工厂编码列表。
    /// </summary>
    [JsonPropertyName("FactoryNames")]
    public List<string> FactoryCodes { get; set; } = new();
}
