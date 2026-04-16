using System.Collections.Generic;
using WearPartsControl.ApplicationServices.SaveInfoService;

namespace WearPartsControl.ApplicationServices.PartServices;

/// <summary>
/// 基地工厂配置的持久化根对象。
/// </summary>
[SaveInfoFile("settings/base-factory")]
public sealed class BaseFactoryOptionsSaveInfo
{
    /// <summary>
    /// 基地配置集合。
    /// </summary>
    public List<BaseFactoryModel> Factories { get; set; } = new();
}
