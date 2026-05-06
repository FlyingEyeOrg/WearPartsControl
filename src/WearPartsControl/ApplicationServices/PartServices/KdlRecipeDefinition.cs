namespace WearPartsControl.ApplicationServices.PartServices;

public sealed class KdlRecipeDefinition
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    public double LowerLimit { get; set; }

    public double UpperLimit { get; set; }

    public bool IsCurrent { get; set; }

    public DateTime? CreatedAt { get; set; } = DateTime.UtcNow;

    public string CreatedBy { get; set; } = string.Empty;

    public string UpdatedBy { get; set; } = string.Empty;

    public DateTime? UpdatedAt { get; set; } = DateTime.UtcNow;
}