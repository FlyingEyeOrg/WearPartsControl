using WearPartsControl.Domain.Entities;

namespace WearPartsControl.Domain.Repositories;

public interface IExceedLimitRecordRepository : IRepository<ExceedLimitRecordEntity, Guid>
{
    Task<IReadOnlyList<ExceedLimitRecordEntity>> ListByClientAppConfigurationAsync(Guid clientAppConfigurationId, CancellationToken cancellationToken = default);

    Task<bool> ExistsForDayAsync(Guid wearPartDefinitionId, string severity, DateTime occurredAt, CancellationToken cancellationToken = default);
}