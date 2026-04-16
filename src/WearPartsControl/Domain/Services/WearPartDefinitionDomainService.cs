using System;
using System.Collections.Generic;
using System.Linq;
using WearPartsControl.Domain.Entities;
using WearPartsControl.Domain.Exceptions;
using WearPartsControl.Domain.Validation;

namespace WearPartsControl.Domain.Services;

public sealed class WearPartDefinitionDomainService
{
    public void ValidateUniquePartNames(IEnumerable<WearPartDefinitionEntity> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);

        var duplicates = definitions
            .Where(x => x is not null)
            .GroupBy(x => x.PartName.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(group => !string.IsNullOrWhiteSpace(group.Key) && group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();

        if (duplicates.Length > 0)
        {
            throw new DomainBusinessException("同一设备配置下不允许存在重复的易损件名称。")
                .WithData("PartNames", string.Join(",", duplicates));
        }
    }

    public void ValidateEntity(WearPartDefinitionEntity definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        definition.EnsureValid();
        DomainValidationRules.MaxLength(definition.PartName.Trim(), 128, nameof(definition.PartName));
        DomainValidationRules.MaxLength(definition.ResourceNumber.Trim(), 128, nameof(definition.ResourceNumber));
    }
}
