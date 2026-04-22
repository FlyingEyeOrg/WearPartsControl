namespace WearPartsControl.ApplicationServices.PartServices;

public interface IToolChangeManagementService
{
    Task<IReadOnlyList<ToolChangeDefinition>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<ToolChangeDefinition> CreateAsync(ToolChangeDefinition definition, CancellationToken cancellationToken = default);

    Task<ToolChangeDefinition> UpdateAsync(ToolChangeDefinition definition, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

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