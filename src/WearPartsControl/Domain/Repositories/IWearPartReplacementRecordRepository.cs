using WearPartsControl.Domain.Entities;

namespace WearPartsControl.Domain.Repositories;

public interface IWearPartReplacementRecordRepository : IRepository<WearPartReplacementRecordEntity, Guid>
{
    Task<IReadOnlyList<WearPartReplacementRecordEntity>> ListByClientAppConfigurationAsync(Guid clientAppConfigurationId, CancellationToken cancellationToken = default);

    Task<WearPartReplacementRecordEntity?> GetLatestByDefinitionAsync(Guid wearPartDefinitionId, CancellationToken cancellationToken = default);

    Task<WearPartReplacementRecordEntity?> GetLatestByCurrentBarcodeAsync(Guid wearPartDefinitionId, string currentBarcode, CancellationToken cancellationToken = default);

    Task<bool> ExistsNewBarcodeAsync(Guid wearPartDefinitionId, string newBarcode, Guid? excludeId = null, CancellationToken cancellationToken = default);
}