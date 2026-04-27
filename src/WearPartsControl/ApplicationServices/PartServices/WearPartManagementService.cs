using WearPartsControl.Domain.Entities;
using WearPartsControl.Domain.Repositories;
using WearPartsControl.ApplicationServices.Localization;
using WearPartsControl.Exceptions;

namespace WearPartsControl.ApplicationServices.PartServices;

public sealed class WearPartManagementService : ApplicationServiceBase, IWearPartManagementService
{
    private static readonly IReadOnlyDictionary<string, string> LifetimeTypeAliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["Meter"] = "记米",
        ["Count"] = "计次",
        ["Time"] = "计时",
        ["记米"] = "记米",
        ["计次"] = "计次",
        ["计时"] = "计时"
    };

    private readonly IClientAppConfigurationRepository _clientAppConfigurationRepository;
    private readonly IWearPartRepository _wearPartRepository;
    private readonly IWearPartTypeRepository _wearPartTypeRepository;
    private readonly IToolChangeRepository _toolChangeRepository;

    public WearPartManagementService(
        ICurrentUserAccessor currentUserAccessor,
        IClientAppConfigurationRepository clientAppConfigurationRepository,
        IWearPartRepository wearPartRepository,
        IWearPartTypeRepository wearPartTypeRepository,
        IToolChangeRepository toolChangeRepository)
        : base(currentUserAccessor)
    {
        _clientAppConfigurationRepository = clientAppConfigurationRepository;
        _wearPartRepository = wearPartRepository;
        _wearPartTypeRepository = wearPartTypeRepository;
        _toolChangeRepository = toolChangeRepository;
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
        await ValidateWearPartTypeAsync(definition.WearPartTypeId, cancellationToken).ConfigureAwait(false);
        await ValidateToolChangeAsync(definition.ToolChangeId, cancellationToken).ConfigureAwait(false);
        var partName = definition.PartName.Trim();

        if (await _wearPartRepository.ExistsPartNameAsync(clientAppConfiguration.Id, partName, cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            throw new UserFriendlyException(LocalizedText.Format("Services.WearPartManagement.DuplicatedPartName", clientAppConfiguration.ResourceNumber, partName));
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
            throw new UserFriendlyException(LocalizedText.Get("Services.WearPartManagement.UpdateIdRequired"));
        }

        ValidateDefinition(definition);

        var entity = await _wearPartRepository.GetByIdAsync(definition.Id, cancellationToken).ConfigureAwait(false)
            ?? throw new EntityNotFoundException(LocalizedText.Format("Services.WearPartManagement.DefinitionNotFoundById", definition.Id));

        var clientAppConfiguration = await ResolveClientAppConfigurationAsync(
                definition.ClientAppConfigurationId,
                definition.ResourceNumber,
                cancellationToken,
                entity.ClientAppConfigurationId)
            .ConfigureAwait(false);

        await ValidateWearPartTypeAsync(definition.WearPartTypeId, cancellationToken).ConfigureAwait(false);
        await ValidateToolChangeAsync(definition.ToolChangeId, cancellationToken).ConfigureAwait(false);

        var partName = definition.PartName.Trim();
        if (await _wearPartRepository.ExistsPartNameAsync(clientAppConfiguration.Id, partName, entity.Id, cancellationToken).ConfigureAwait(false))
        {
            throw new UserFriendlyException(LocalizedText.Format("Services.WearPartManagement.DuplicatedPartName", clientAppConfiguration.ResourceNumber, partName));
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

        var normalizedSource = NormalizeRequired(sourceResourceNumber, LocalizedText.Get("Services.WearPartManagement.SourceResourceNumberRequired"));
        var normalizedTarget = NormalizeRequired(targetResourceNumber, LocalizedText.Get("Services.WearPartManagement.TargetResourceNumberRequired"));

        if (string.Equals(normalizedSource, normalizedTarget, StringComparison.OrdinalIgnoreCase))
        {
            throw new UserFriendlyException(LocalizedText.Get("Services.WearPartManagement.SourceTargetSame"));
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
            throw new UserFriendlyException(LocalizedText.Format("Services.WearPartManagement.CopyDuplicatePartNames", targetConfiguration.ResourceNumber, string.Join(",", duplicatePartNames)));
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

        throw new UserFriendlyException(LocalizedText.Get("Services.WearPartManagement.ConfigurationOrResourceRequired"));
    }

    private async Task<ClientAppConfigurationEntity> GetRequiredClientAppConfigurationAsync(Guid clientAppConfigurationId, CancellationToken cancellationToken)
    {
        var clientAppConfiguration = await _clientAppConfigurationRepository.GetByIdAsync(clientAppConfigurationId, cancellationToken).ConfigureAwait(false);
        if (clientAppConfiguration is null)
        {
            throw new EntityNotFoundException(LocalizedText.Format("Services.WearPartManagement.ClientConfigurationNotFoundById", clientAppConfigurationId));
        }

        return clientAppConfiguration;
    }

    private async Task<ClientAppConfigurationEntity> GetRequiredClientAppConfigurationByResourceNumberAsync(string resourceNumber, CancellationToken cancellationToken)
    {
        var normalized = NormalizeRequired(resourceNumber, LocalizedText.Get("Services.Common.ResourceNumberRequired"));
        var clientAppConfiguration = await _clientAppConfigurationRepository.GetByResourceNumberAsync(normalized, cancellationToken).ConfigureAwait(false);
        if (clientAppConfiguration is null)
        {
            throw new EntityNotFoundException(LocalizedText.Format("Services.WearPartManagement.ClientConfigurationNotFoundByResourceNumber", normalized));
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
            WearPartTypeId = sourceDefinition.WearPartTypeId,
            ToolChangeId = sourceDefinition.ToolChangeId,
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
        entity.LifetimeType = NormalizeLifetimeType(definition.LifetimeType);
        entity.WearPartTypeId = definition.WearPartTypeId;
        entity.ToolChangeId = definition.ToolChangeId;
        entity.PlcZeroClearAddress = NormalizeOptional(definition.PlcZeroClearAddress);
        entity.BarcodeWriteAddress = NormalizeOptional(definition.BarcodeWriteAddress);
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
            WearPartTypeId = entity.WearPartTypeId,
            WearPartTypeCode = entity.WearPartType?.Code ?? WearPartTypeCodes.Uncategorized,
            WearPartTypeName = entity.WearPartType?.Name ?? "未分类",
            ToolChangeId = entity.ToolChangeId,
            PlcZeroClearAddress = entity.PlcZeroClearAddress,
            BarcodeWriteAddress = entity.BarcodeWriteAddress,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedBy = entity.UpdatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt
        };
    }

    private static void ValidateDefinition(WearPartDefinition definition)
    {
        NormalizeRequired(definition.PartName, LocalizedText.Get("Services.WearPartManagement.PartNameRequired"));
        NormalizeRequired(definition.InputMode, LocalizedText.Get("Services.WearPartManagement.InputModeRequired"));
        NormalizeRequired(definition.CurrentValueAddress, LocalizedText.Get("Services.WearPartManagement.CurrentValueAddressRequired"));
        NormalizeRequired(definition.CurrentValueDataType, LocalizedText.Get("Services.WearPartManagement.CurrentValueDataTypeRequired"));
        NormalizeRequired(definition.WarningValueAddress, LocalizedText.Get("Services.WearPartManagement.WarningValueAddressRequired"));
        NormalizeRequired(definition.WarningValueDataType, LocalizedText.Get("Services.WearPartManagement.WarningValueDataTypeRequired"));
        NormalizeRequired(definition.ShutdownValueAddress, LocalizedText.Get("Services.WearPartManagement.ShutdownValueAddressRequired"));
        NormalizeRequired(definition.ShutdownValueDataType, LocalizedText.Get("Services.WearPartManagement.ShutdownValueDataTypeRequired"));
        NormalizeLifetimeType(definition.LifetimeType);

        if (!definition.WearPartTypeId.HasValue || definition.WearPartTypeId.Value == Guid.Empty)
        {
            throw new UserFriendlyException(LocalizedText.Get("Services.WearPartManagement.WearPartTypeRequired"));
        }

        if (definition.CodeMinLength < 0)
        {
            throw new UserFriendlyException(LocalizedText.Get("Services.WearPartManagement.CodeMinLengthRangeInvalid"));
        }

        if (definition.CodeMaxLength <= 0)
        {
            throw new UserFriendlyException(LocalizedText.Get("Services.WearPartManagement.CodeMaxLengthRangeInvalid"));
        }

        if (definition.CodeMinLength > definition.CodeMaxLength)
        {
            throw new UserFriendlyException(LocalizedText.Get("Services.WearPartManagement.CodeLengthRangeInverted"));
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

    private static string NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private async Task ValidateWearPartTypeAsync(Guid? wearPartTypeId, CancellationToken cancellationToken)
    {
        if (!wearPartTypeId.HasValue || wearPartTypeId.Value == Guid.Empty)
        {
            throw new UserFriendlyException(LocalizedText.Get("Services.WearPartManagement.WearPartTypeRequired"));
        }

        var exists = await _wearPartTypeRepository.GetByIdAsync(wearPartTypeId.Value, cancellationToken).ConfigureAwait(false);
        if (exists is null)
        {
            throw new EntityNotFoundException(LocalizedText.Format("Services.WearPartManagement.WearPartTypeNotFoundById", wearPartTypeId.Value));
        }
    }

    private async Task ValidateToolChangeAsync(Guid? toolChangeId, CancellationToken cancellationToken)
    {
        if (!toolChangeId.HasValue || toolChangeId.Value == Guid.Empty)
        {
            return;
        }

        var exists = await _toolChangeRepository.GetByIdAsync(toolChangeId.Value, cancellationToken).ConfigureAwait(false);
        if (exists is null)
        {
            throw new EntityNotFoundException(LocalizedText.Format("Services.ToolChangeManagement.NotFoundById", toolChangeId.Value));
        }
    }

    private static string NormalizeLifetimeType(string? value)
    {
        var normalized = NormalizeRequired(value, LocalizedText.Get("Services.WearPartManagement.LifetimeTypeRequired"));
        return LifetimeTypeAliases.TryGetValue(normalized, out var alias)
            ? alias
            : normalized;
    }
}