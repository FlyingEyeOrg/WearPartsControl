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
        DomainValidationRules.MaxLength(definition.InputMode.Trim(), 64, nameof(definition.InputMode));
        DomainValidationRules.MaxLength(definition.CurrentValueAddress.Trim(), 128, nameof(definition.CurrentValueAddress));
        DomainValidationRules.MaxLength(definition.CurrentValueDataType.Trim(), 64, nameof(definition.CurrentValueDataType));
        DomainValidationRules.MaxLength(definition.WarningValueAddress.Trim(), 128, nameof(definition.WarningValueAddress));
        DomainValidationRules.MaxLength(definition.WarningValueDataType.Trim(), 64, nameof(definition.WarningValueDataType));
        DomainValidationRules.MaxLength(definition.ShutdownValueAddress.Trim(), 128, nameof(definition.ShutdownValueAddress));
        DomainValidationRules.MaxLength(definition.ShutdownValueDataType.Trim(), 64, nameof(definition.ShutdownValueDataType));
        DomainValidationRules.MaxLength(definition.LifetimeType.Trim(), 64, nameof(definition.LifetimeType));
        DomainValidationRules.MaxLength(definition.PlcZeroClearAddress.Trim(), 128, nameof(definition.PlcZeroClearAddress));
        DomainValidationRules.MaxLength(definition.BarcodeWriteAddress.Trim(), 128, nameof(definition.BarcodeWriteAddress));

        if (definition.CodeMinLength < 0)
        {
            throw new DomainBusinessException("条码最小长度不能小于 0。");
        }

        if (definition.CodeMaxLength <= 0)
        {
            throw new DomainBusinessException("条码最大长度必须大于 0。");
        }

        if (definition.CodeMinLength > definition.CodeMaxLength)
        {
            throw new DomainBusinessException("条码最小长度不能大于最大长度。");
        }
    }
}
