namespace WearPartsControl.ApplicationServices.PartServices;

public sealed class KdlRecipeSettingsState
{
    public Guid? CurrentRecipeId { get; init; }

    public IReadOnlyList<KdlRecipeDefinition> Recipes { get; init; } = [];
}