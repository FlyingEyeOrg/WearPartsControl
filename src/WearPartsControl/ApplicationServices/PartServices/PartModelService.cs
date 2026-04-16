using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WearPartsControl.ApplicationServices.SaveInfoService;

namespace WearPartsControl.ApplicationServices.PartServices;

/// <summary>
/// 基地工厂配置服务，负责读取、合并和保存分组后的数据。
/// </summary>
public sealed class PartModelService : IPartModelService
{
    /// <summary>
    /// 保存信息仓储。
    /// </summary>
    private readonly ISaveInfoStore _saveInfoStore;

    /// <summary>
    /// 初始化服务实例。
    /// </summary>
    public PartModelService(ISaveInfoStore saveInfoStore)
    {
        _saveInfoStore = saveInfoStore;
    }

    /// <summary>
    /// 读取并归一化基地工厂配置，确保同一基地下的工厂编码合并为数组。
    /// </summary>
    public async ValueTask<IReadOnlyList<SiteFactory>> GetSiteFactoryModelsAsync(CancellationToken cancellationToken = default)
    {
        var options = await _saveInfoStore.ReadAsync<SiteFactoryOptionsSaveInfo>(cancellationToken).ConfigureAwait(false);

        return NormalizeFactories(options.Factories);
    }

    /// <summary>
    /// 保存基地工厂配置，并在写入前完成分组与去重。
    /// </summary>
    public ValueTask SaveSiteFactoryModelsAsync(IReadOnlyCollection<SiteFactory> factories, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(factories);

        var options = new SiteFactoryOptionsSaveInfo
        {
            Factories = NormalizeFactories(factories).ToList()
        };

        return _saveInfoStore.WriteAsync(options, cancellationToken);
    }

    /// <summary>
    /// 按基地编码和基地名称合并同类数据，并对工厂编码进行去重排序。
    /// </summary>
    private static IReadOnlyList<SiteFactory> NormalizeFactories(IEnumerable<SiteFactory> factories)
    {
        return factories
            .Where(factory => factory is not null)
            .Where(factory => !string.IsNullOrWhiteSpace(factory.Site) && !string.IsNullOrWhiteSpace(factory.SiteName))
            .GroupBy(factory => NormalizeGroupKey(factory.Site, factory.SiteName), StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var first = group.First();
                var factoryNames = group
                    .SelectMany(factory => factory.FactoryNames ?? [])
                    .Where(factoryName => !string.IsNullOrWhiteSpace(factoryName))
                    .Select(factoryName => factoryName.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(factoryName => factoryName, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                return new SiteFactory
                {
                    Site = first.Site.Trim(),
                    SiteName = first.SiteName.Trim(),
                    FactoryNames = factoryNames
                };
            })
            .Where(factory => factory.FactoryNames.Count > 0)
            .OrderBy(factory => factory.Site, StringComparer.OrdinalIgnoreCase)
            .ThenBy(factory => factory.SiteName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <summary>
    /// 构造用于分组的稳定键。
    /// </summary>
    private static string NormalizeGroupKey(string baseCode, string baseName)
    {
        return $"{baseCode.Trim()}||{baseName.Trim()}";
    }
}
