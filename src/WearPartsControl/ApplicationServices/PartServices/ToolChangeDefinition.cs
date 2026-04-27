namespace WearPartsControl.ApplicationServices.PartServices;

public sealed class ToolChangeDefinition
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;

    public DateTime? CreatedAt { get; set; }

    public string CreatedBy { get; set; } = string.Empty;

    public string UpdatedBy { get; set; } = string.Empty;

    public DateTime? UpdatedAt { get; set; }

    public string DisplayText => string.IsNullOrWhiteSpace(Code) ? Name : $"{Name} ({Code})";
}