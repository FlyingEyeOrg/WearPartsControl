using System.Collections.Generic;
using System.Text.Json.Serialization;
using WearPartsControl.ApplicationServices.SaveInfoService;

namespace WearPartsControl.ApplicationServices.PartServices;

/// <summary>
/// 基地工厂配置的持久化根对象。
/// </summary>
[SaveInfoFile("site-factory")]
public sealed class SiteFactoryOptionsSaveInfo
{
    /// <summary>
    /// 基地配置集合。
    /// </summary>
    [JsonPropertyName("Factories")]
    public List<SiteFactoryMapping> SiteFactories { get; set; } = new();
}
