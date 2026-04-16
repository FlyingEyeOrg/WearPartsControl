using WearPartsControl.Domain.Entities;

namespace WearPartsControl.Domain.Repositories;

public interface IWearPartReplacementRecordRepository : IRepository<WearPartReplacementRecordEntity, Guid>
{
    Task<IReadOnlyList<WearPartReplacementRecordEntity>> ListByBasicConfigurationAsync(Guid basicConfigurationId, CancellationToken cancellationToken = default);

    Task<WearPartReplacementRecordEntity?> GetLatestByDefinitionAsync(Guid wearPartDefinitionId, CancellationToken cancellationToken = default);

    Task<bool> ExistsNewBarcodeAsync(Guid wearPartDefinitionId, string newBarcode, Guid? excludeId = null, CancellationToken cancellationToken = default);
}