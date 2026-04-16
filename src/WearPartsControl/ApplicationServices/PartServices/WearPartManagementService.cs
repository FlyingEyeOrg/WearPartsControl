using WearPartsControl.Domain.Entities;
using WearPartsControl.Domain.Repositories;
using WearPartsControl.Exceptions;

namespace WearPartsControl.ApplicationServices.PartServices;

public sealed class WearPartManagementService : ApplicationService, IWearPartManagementService
{
    private readonly IClientAppConfigurationRepository _clientAppConfigurationRepository;
    private readonly IWearPartRepository _wearPartRepository;

    public WearPartManagementService(
        ICurrentUserAccessor currentUserAccessor,
        IClientAppConfigurationRepository clientAppConfigurationRepository,
        IWearPartRepository wearPartRepository)
        : base(currentUserAccessor)
    {
        _clientAppConfigurationRepository = clientAppConfigurationRepository;
        _wearPartRepository = wearPartRepository;
    }

    public async Task<IReadOnlyList<WearPartDefinition>> GetDefinitionsByClientAppConfigurationAsync(Guid clientAppConfigurationId, CancellationToken cancellationToken = default)
    {
        if (clientAppConfigurationId == Guid.Empty)
        {
            return [];
        }

        var entities = await _wearPartRepository.ListByClientAppConfigurationAsync(clientAppConfigurationId, cancellationToken).ConfigureAwait(false);
        return entities.Select(MapToModel).ToArray();
    }

    public async Task<IReadOnlyList<WearPartDefinition>> GetDefinitionsByResourceNumberAsync(string resourceNumber, CancellationToken cancellationToken = default)
    {
        var clientAppConfiguration = await GetRequiredClientAppConfigurationByResourceNumberAsync(resourceNumber, cancellationToken).ConfigureAwait(false);
        return await GetDefinitionsByClientAppConfigurationAsync(clientAppConfiguration.Id, cancellationToken).ConfigureAwait(false);
    }

    public async Task<WearPartDefinition?> GetDefinitionAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
        {
            return null;
        }

        var entity = await _wearPartRepository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        return entity is null ? null : MapToModel(entity);
    }

    public async Task<WearPartDefinition> CreateDefinitionAsync(WearPartDefinition definition, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);

        GetRequiredCurrentUserId();
        ValidateDefinition(definition);

        var clientAppConfiguration = await ResolveClientAppConfigurationAsync(definition.ClientAppConfigurationId, definition.ResourceNumber, cancellationToken).ConfigureAwait(false);
        var partName = definition.PartName.Trim();

        if (await _wearPartRepository.ExistsPartNameAsync(clientAppConfiguration.Id, partName, cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            throw new UserFriendlyException($"资源号 {clientAppConfiguration.ResourceNumber} 下已存在名称为 {partName} 的易损件定义。");
        }

        var entity = new WearPartDefinitionEntity();
        ApplyDefinition(entity, definition, clientAppConfiguration);

        await _wearPartRepository.AddAsync(entity, cancellationToken).ConfigureAwait(false);
        await _wearPartRepository.UnitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return MapToModel(entity);
    }

    public async Task<WearPartDefinition> UpdateDefinitionAsync(WearPartDefinition definition, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);

        GetRequiredCurrentUserId();

        if (definition.Id == Guid.Empty)
        {
            throw new UserFriendlyException("更新易损件定义时必须提供有效的主键。");
        }

        ValidateDefinition(definition);

        var entity = await _wearPartRepository.GetByIdAsync(definition.Id, cancellationToken).ConfigureAwait(false)
            ?? throw new EntityNotFoundException($"未找到主键为 {definition.Id} 的易损件定义。");

        var clientAppConfiguration = await ResolveClientAppConfigurationAsync(
                definition.ClientAppConfigurationId,
                definition.ResourceNumber,
                cancellationToken,
                entity.ClientAppConfigurationId)
            .ConfigureAwait(false);

        var partName = definition.PartName.Trim();
        if (await _wearPartRepository.ExistsPartNameAsync(clientAppConfiguration.Id, partName, entity.Id, cancellationToken).ConfigureAwait(false))
        {
            throw new UserFriendlyException($"资源号 {clientAppConfiguration.ResourceNumber} 下已存在名称为 {partName} 的易损件定义。");
        }

        ApplyDefinition(entity, definition, clientAppConfiguration);

        await _wearPartRepository.UpdateAsync(entity, cancellationToken).ConfigureAwait(false);
        await _wearPartRepository.UnitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return MapToModel(entity);
    }

    public async Task DeleteDefinitionAsync(Guid id, CancellationToken cancellationToken = default)
    {
        GetRequiredCurrentUserId();

        if (id == Guid.Empty)
        {
            return;
        }

        await _wearPartRepository.DeleteAsync(id, cancellationToken).ConfigureAwait(false);
        await _wearPartRepository.UnitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<int> CopyDefinitionsAsync(string sourceResourceNumber, string targetResourceNumber, CancellationToken cancellationToken = default)
    {
        GetRequiredCurrentUserId();

        var normalizedSource = NormalizeRequired(sourceResourceNumber, "源资源号不能为空。");
        var normalizedTarget = NormalizeRequired(targetResourceNumber, "目标资源号不能为空。");

        if (string.Equals(normalizedSource, normalizedTarget, StringComparison.OrdinalIgnoreCase))
        {
            throw new UserFriendlyException("源资源号和目标资源号不能相同。");
        }

        var sourceConfiguration = await GetRequiredClientAppConfigurationByResourceNumberAsync(normalizedSource, cancellationToken).ConfigureAwait(false);
        var targetConfiguration = await GetRequiredClientAppConfigurationByResourceNumberAsync(normalizedTarget, cancellationToken).ConfigureAwait(false);

        var sourceDefinitions = await _wearPartRepository.ListByClientAppConfigurationAsync(sourceConfiguration.Id, cancellationToken).ConfigureAwait(false);
        if (sourceDefinitions.Count == 0)
        {
            return 0;
        }

        var targetDefinitions = await _wearPartRepository.ListByClientAppConfigurationAsync(targetConfiguration.Id, cancellationToken).ConfigureAwait(false);
        var duplicatePartNames = sourceDefinitions
            .Select(x => x.PartName.Trim())
            .Intersect(targetDefinitions.Select(x => x.PartName.Trim()), StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (duplicatePartNames.Length > 0)
        {
            throw new UserFriendlyException($"目标资源号 {targetConfiguration.ResourceNumber} 已存在以下易损件定义：{string.Join(",", duplicatePartNames)}。");
        }

        foreach (var sourceDefinition in sourceDefinitions)
        {
            var copy = CloneDefinition(sourceDefinition, targetConfiguration);
            await _wearPartRepository.AddAsync(copy, cancellationToken).ConfigureAwait(false);
        }

        await _wearPartRepository.UnitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return sourceDefinitions.Count;
    }

    private async Task<ClientAppConfigurationEntity> ResolveClientAppConfigurationAsync(
        Guid clientAppConfigurationId,
        string resourceNumber,
        CancellationToken cancellationToken,
        Guid? fallbackClientAppConfigurationId = null)
    {
        if (clientAppConfigurationId != Guid.Empty)
        {
            return await GetRequiredClientAppConfigurationAsync(clientAppConfigurationId, cancellationToken).ConfigureAwait(false);
        }

        if (!string.IsNullOrWhiteSpace(resourceNumber))
        {
            return await GetRequiredClientAppConfigurationByResourceNumberAsync(resourceNumber, cancellationToken).ConfigureAwait(false);
        }

        if (fallbackClientAppConfigurationId.HasValue && fallbackClientAppConfigurationId.Value != Guid.Empty)
        {
            return await GetRequiredClientAppConfigurationAsync(fallbackClientAppConfigurationId.Value, cancellationToken).ConfigureAwait(false);
        }

        throw new UserFriendlyException("易损件定义必须关联有效的客户端配置或资源号。");
    }

    private async Task<ClientAppConfigurationEntity> GetRequiredClientAppConfigurationAsync(Guid clientAppConfigurationId, CancellationToken cancellationToken)
    {
        var clientAppConfiguration = await _clientAppConfigurationRepository.GetByIdAsync(clientAppConfigurationId, cancellationToken).ConfigureAwait(false);
        if (clientAppConfiguration is null)
        {
            throw new EntityNotFoundException($"未找到主键为 {clientAppConfigurationId} 的客户端配置。");
        }

        return clientAppConfiguration;
    }

    private async Task<ClientAppConfigurationEntity> GetRequiredClientAppConfigurationByResourceNumberAsync(string resourceNumber, CancellationToken cancellationToken)
    {
        var normalized = NormalizeRequired(resourceNumber, "资源号不能为空。");
        var clientAppConfiguration = await _clientAppConfigurationRepository.GetByResourceNumberAsync(normalized, cancellationToken).ConfigureAwait(false);
        if (clientAppConfiguration is null)
        {
            throw new EntityNotFoundException($"未找到资源号为 {normalized} 的客户端配置。");
        }

        return clientAppConfiguration;
    }

    private static WearPartDefinitionEntity CloneDefinition(WearPartDefinitionEntity sourceDefinition, ClientAppConfigurationEntity targetConfiguration)
    {
        return new WearPartDefinitionEntity
        {
            ClientAppConfigurationId = targetConfiguration.Id,
            ResourceNumber = targetConfiguration.ResourceNumber,
            PartName = sourceDefinition.PartName,
            InputMode = sourceDefinition.InputMode,
            CurrentValueAddress = sourceDefinition.CurrentValueAddress,
            CurrentValueDataType = sourceDefinition.CurrentValueDataType,
            WarningValueAddress = sourceDefinition.WarningValueAddress,
            WarningValueDataType = sourceDefinition.WarningValueDataType,
            ShutdownValueAddress = sourceDefinition.ShutdownValueAddress,
            ShutdownValueDataType = sourceDefinition.ShutdownValueDataType,
            IsShutdown = sourceDefinition.IsShutdown,
            CodeMinLength = sourceDefinition.CodeMinLength,
            CodeMaxLength = sourceDefinition.CodeMaxLength,
            LifetimeType = sourceDefinition.LifetimeType,
            PlcZeroClearAddress = sourceDefinition.PlcZeroClearAddress,
            BarcodeWriteAddress = sourceDefinition.BarcodeWriteAddress
        };
    }

    private static void ApplyDefinition(WearPartDefinitionEntity entity, WearPartDefinition definition, ClientAppConfigurationEntity clientAppConfiguration)
    {
        entity.ClientAppConfigurationId = clientAppConfiguration.Id;
        entity.ResourceNumber = clientAppConfiguration.ResourceNumber;
        entity.PartName = definition.PartName.Trim();
        entity.InputMode = definition.InputMode.Trim();
        entity.CurrentValueAddress = definition.CurrentValueAddress.Trim();
        entity.CurrentValueDataType = definition.CurrentValueDataType.Trim();
        entity.WarningValueAddress = definition.WarningValueAddress.Trim();
        entity.WarningValueDataType = definition.WarningValueDataType.Trim();
        entity.ShutdownValueAddress = definition.ShutdownValueAddress.Trim();
        entity.ShutdownValueDataType = definition.ShutdownValueDataType.Trim();
        entity.IsShutdown = definition.IsShutdown;
        entity.CodeMinLength = definition.CodeMinLength;
        entity.CodeMaxLength = definition.CodeMaxLength;
        entity.LifetimeType = definition.LifetimeType.Trim();
        entity.PlcZeroClearAddress = definition.PlcZeroClearAddress.Trim();
        entity.BarcodeWriteAddress = definition.BarcodeWriteAddress.Trim();
    }

    private static WearPartDefinition MapToModel(WearPartDefinitionEntity entity)
    {
        return new WearPartDefinition
        {
            Id = entity.Id,
            ClientAppConfigurationId = entity.ClientAppConfigurationId,
            ResourceNumber = entity.ResourceNumber,
            PartName = entity.PartName,
            InputMode = entity.InputMode,
            CurrentValueAddress = entity.CurrentValueAddress,
            CurrentValueDataType = entity.CurrentValueDataType,
            WarningValueAddress = entity.WarningValueAddress,
            WarningValueDataType = entity.WarningValueDataType,
            ShutdownValueAddress = entity.ShutdownValueAddress,
            ShutdownValueDataType = entity.ShutdownValueDataType,
            IsShutdown = entity.IsShutdown,
            CodeMinLength = entity.CodeMinLength,
            CodeMaxLength = entity.CodeMaxLength,
            LifetimeType = entity.LifetimeType,
            PlcZeroClearAddress = entity.PlcZeroClearAddress,
            BarcodeWriteAddress = entity.BarcodeWriteAddress,
            UpdatedAt = entity.UpdatedAt
        };
    }

    private static void ValidateDefinition(WearPartDefinition definition)
    {
        NormalizeRequired(definition.PartName, "易损件名称不能为空。");
        NormalizeRequired(definition.InputMode, "输入方式不能为空。");
        NormalizeRequired(definition.CurrentValueAddress, "当前值点位不能为空。");
        NormalizeRequired(definition.CurrentValueDataType, "当前值点位类型不能为空。");
        NormalizeRequired(definition.WarningValueAddress, "预警值点位不能为空。");
        NormalizeRequired(definition.WarningValueDataType, "预警值点位类型不能为空。");
        NormalizeRequired(definition.ShutdownValueAddress, "停机值点位不能为空。");
        NormalizeRequired(definition.ShutdownValueDataType, "停机值点位类型不能为空。");
        NormalizeRequired(definition.LifetimeType, "寿命类型不能为空。");
        NormalizeRequired(definition.PlcZeroClearAddress, "PLC 清零点位不能为空。");
        NormalizeRequired(definition.BarcodeWriteAddress, "条码写入点位不能为空。");

        if (definition.CodeMinLength < 0)
        {
            throw new UserFriendlyException("条码最小长度不能小于 0。");
        }

        if (definition.CodeMaxLength <= 0)
        {
            throw new UserFriendlyException("条码最大长度必须大于 0。");
        }

        if (definition.CodeMinLength > definition.CodeMaxLength)
        {
            throw new UserFriendlyException("条码最小长度不能大于最大长度。");
        }
    }

    private static string NormalizeRequired(string? value, string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new UserFriendlyException(errorMessage);
        }

        return value.Trim();
    }
}