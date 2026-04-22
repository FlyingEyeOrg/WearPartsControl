using WearPartsControl.Domain.Entities;

namespace WearPartsControl.Domain.Repositories;

public interface IToolChangeRepository : IRepository<ToolChangeEntity, Guid>
{
    Task<bool> ExistsNameAsync(string name, Guid? excludeId = null, CancellationToken cancellationToken = default);

    Task<bool> ExistsCodeAsync(string code, Guid? excludeId = null, CancellationToken cancellationToken = default);
}