using WearPartsControl.Domain.Entities;

namespace WearPartsControl.Domain.Repositories;

public interface IWearPartTypeRepository : IRepository<WearPartTypeEntity, Guid>
{
    Task<WearPartTypeEntity?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);
}