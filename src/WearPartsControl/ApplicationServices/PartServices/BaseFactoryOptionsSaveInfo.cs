using System.Collections.Generic;
using WearPartsControl.ApplicationServices.SaveInfoService;

namespace WearPartsControl.ApplicationServices.PartServices;

[SaveInfoFile("settings/base-factory")]
public sealed class BaseFactoryOptionsSaveInfo
{
    public List<BaseFactoryModel> Factories { get; set; } = new();
}
