namespace WearPartsControl.ApplicationServices.PartServices;

public interface IWearPartManagementService
{
    Task<IReadOnlyList<WearPartDefinition>> GetDefinitionsByBasicConfigurationAsync(Guid basicConfigurationId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WearPartDefinition>> GetDefinitionsByResourceNumberAsync(string resourceNumber, CancellationToken cancellationToken = default);

    Task<WearPartDefinition?> GetDefinitionAsync(Guid id, CancellationToken cancellationToken = default);

    Task<WearPartDefinition> CreateDefinitionAsync(WearPartDefinition definition, CancellationToken cancellationToken = default);

    Task<WearPartDefinition> UpdateDefinitionAsync(WearPartDefinition definition, CancellationToken cancellationToken = default);

    Task DeleteDefinitionAsync(Guid id, CancellationToken cancellationToken = default);

    Task<int> CopyDefinitionsAsync(string sourceResourceNumber, string targetResourceNumber, CancellationToken cancellationToken = default);
}