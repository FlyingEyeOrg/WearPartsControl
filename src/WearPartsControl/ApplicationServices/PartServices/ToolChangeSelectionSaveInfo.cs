using WearPartsControl.ApplicationServices.SaveInfoService;

namespace WearPartsControl.ApplicationServices.PartServices;

[SaveInfoFile("tool-change-selection")]
public sealed class ToolChangeSelectionSaveInfo
{
    public List<ToolChangeSelectionItem> Items { get; set; } = [];

    public List<string> RecentToolCodes { get; set; } = [];
}