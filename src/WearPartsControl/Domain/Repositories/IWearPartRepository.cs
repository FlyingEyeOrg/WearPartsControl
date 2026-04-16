using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WearPartsControl.Domain.Entities;

namespace WearPartsControl.Domain.Repositories;

public interface IWearPartRepository : IRepository<WearPartDefinitionEntity, Guid>
{
    Task<IReadOnlyList<WearPartDefinitionEntity>> ListByBasicConfigurationAsync(Guid basicConfigurationId, CancellationToken cancellationToken = default);

    Task<bool> ExistsPartNameAsync(Guid basicConfigurationId, string partName, Guid? excludeId = null, CancellationToken cancellationToken = default);

    Task AddRangeAsync(IReadOnlyCollection<WearPartDefinitionEntity> entities, CancellationToken cancellationToken = default);
}
