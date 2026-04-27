namespace WearPartsControl.ApplicationServices.PartServices;

public sealed class ToolChangeSelectionItem
{
    public Guid WearPartDefinitionId { get; set; }

    public string SelectedToolCode { get; set; } = string.Empty;
}