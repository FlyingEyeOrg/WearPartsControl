using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace WearPartsControl.ApplicationServices.PartServices;

/// <summary>
/// Part 模型配置服务。
/// </summary>
public interface IPartModelService
{
    /// <summary>
    /// 读取按基地分组后的工厂配置。
    /// </summary>
    ValueTask<IReadOnlyList<SiteFactoryMapping>> GetSiteFactoryModelsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 保存按基地分组后的工厂配置。
    /// </summary>
    ValueTask SaveSiteFactoryModelsAsync(IReadOnlyCollection<SiteFactoryMapping> siteFactories, CancellationToken cancellationToken = default);
}
