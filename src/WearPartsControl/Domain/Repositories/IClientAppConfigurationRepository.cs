using System;
using System.Threading;
using System.Threading.Tasks;
using WearPartsControl.Domain.Entities;

namespace WearPartsControl.Domain.Repositories;

public interface IClientAppConfigurationRepository : IRepository<ClientAppConfigurationEntity, Guid>
{
    Task<ClientAppConfigurationEntity?> GetByResourceNumberAsync(string resourceNumber, CancellationToken cancellationToken = default);

    Task<bool> ExistsByResourceNumberAsync(string resourceNumber, Guid? excludeId = null, CancellationToken cancellationToken = default);
}
