namespace WearPartsControl.ApplicationServices.PartServices;

public interface IToolChangeSelectionService
{
    ValueTask<ToolChangeSelectionState> GetStateAsync(Guid wearPartDefinitionId, CancellationToken cancellationToken = default);

    ValueTask SaveSelectionAsync(Guid wearPartDefinitionId, string toolCode, CancellationToken cancellationToken = default);
}