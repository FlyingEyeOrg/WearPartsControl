using System.Collections.Generic;

namespace WearPartsControl.ApplicationServices.PartServices;

/// <summary>
/// 基地与工厂的分组配置模型。
/// </summary>
public sealed class SiteFactory
{
    /// <summary>
    /// 基地编码。
    /// </summary>
    public string Site { get; set; } = string.Empty;

    /// <summary>
    /// 基地名称。
    /// </summary>
    public string SiteName { get; set; } = string.Empty;

    /// <summary>
    /// 该基地下的工厂编码列表。
    /// </summary>
    public List<string> FactoryNames { get; set; } = new();
}
