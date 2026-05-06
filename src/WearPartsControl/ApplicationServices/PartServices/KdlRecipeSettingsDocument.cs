using WearPartsControl.ApplicationServices.SaveInfoService;

namespace WearPartsControl.ApplicationServices.PartServices;

[SaveInfoFile("kdl-recipe-settings.json")]
public sealed class KdlRecipeSettingsDocument
{
    public Guid? CurrentRecipeId { get; set; }

    public List<KdlRecipeRecord> Recipes { get; set; } = [];
}

public sealed class KdlRecipeRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    public double LowerLimit { get; set; }

    public double UpperLimit { get; set; }

    public DateTime? CreatedAt { get; set; } = DateTime.UtcNow;

    public string CreatedBy { get; set; } = string.Empty;

    public string UpdatedBy { get; set; } = string.Empty;

    public DateTime? UpdatedAt { get; set; } = DateTime.UtcNow;
}