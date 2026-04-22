namespace WearPartsControl.ApplicationServices.PartServices;

public interface IToolChangeSelectionService
{
    ValueTask<ToolChangeSelectionState> GetStateAsync(Guid wearPartDefinitionId, CancellationToken cancellationToken = default);

    ValueTask SaveSelectionAsync(Guid wearPartDefinitionId, string toolCode, CancellationToken cancellationToken = default);
}

public sealed class ToolChangeSelectionState
{
    public string SelectedToolCode { get; init; } = string.Empty;

    public IReadOnlyList<string> RecentToolCodes { get; init; } = Array.Empty<string>();
}