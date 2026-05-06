namespace WearPartsControl.ApplicationServices.PartServices;

public interface IKdlRecipeManagementService
{
    ValueTask<KdlRecipeSettingsState> GetAsync(CancellationToken cancellationToken = default);

    ValueTask<KdlRecipeDefinition?> GetCurrentRecipeAsync(CancellationToken cancellationToken = default);

    ValueTask<KdlRecipeDefinition> CreateAsync(KdlRecipeDefinition definition, CancellationToken cancellationToken = default);

    ValueTask<KdlRecipeDefinition> UpdateAsync(KdlRecipeDefinition definition, CancellationToken cancellationToken = default);

    ValueTask SetCurrentAsync(Guid recipeId, CancellationToken cancellationToken = default);

    ValueTask DeleteAsync(Guid recipeId, CancellationToken cancellationToken = default);
}