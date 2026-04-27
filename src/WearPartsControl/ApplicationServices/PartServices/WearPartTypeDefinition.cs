namespace WearPartsControl.ApplicationServices.PartServices;

public sealed class WearPartTypeDefinition
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string DisplayText => Name;
}