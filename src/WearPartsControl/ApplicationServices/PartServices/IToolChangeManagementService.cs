namespace WearPartsControl.ApplicationServices.PartServices;

public interface IToolChangeManagementService
{
    Task<IReadOnlyList<ToolChangeDefinition>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<ToolChangeDefinition> CreateAsync(ToolChangeDefinition definition, CancellationToken cancellationToken = default);

    Task<ToolChangeDefinition> UpdateAsync(ToolChangeDefinition definition, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}