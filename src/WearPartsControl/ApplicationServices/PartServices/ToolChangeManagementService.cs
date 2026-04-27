using WearPartsControl.ApplicationServices.Localization;
using WearPartsControl.Domain.Entities;
using WearPartsControl.Domain.Repositories;
using WearPartsControl.Exceptions;

namespace WearPartsControl.ApplicationServices.PartServices;

public sealed class ToolChangeManagementService : ApplicationServiceBase, IToolChangeManagementService
{
    private readonly IToolChangeRepository _toolChangeRepository;

    public ToolChangeManagementService(ICurrentUserAccessor currentUserAccessor, IToolChangeRepository toolChangeRepository)
        : base(currentUserAccessor)
    {
        _toolChangeRepository = toolChangeRepository;
    }

    public async Task<IReadOnlyList<ToolChangeDefinition>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _toolChangeRepository.ListAsync(cancellationToken).ConfigureAwait(false);
        return entities.Select(Map).ToArray();
    }

    public async Task<ToolChangeDefinition> CreateAsync(ToolChangeDefinition definition, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);

        GetRequiredCurrentUserId();
        var name = NormalizeRequired(definition.Name, LocalizedText.Get("Services.ToolChangeManagement.NameRequired"));
        var code = NormalizeRequired(definition.Code, LocalizedText.Get("Services.ToolChangeManagement.CodeRequired"));

        if (await _toolChangeRepository.ExistsNameAsync(name, cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            throw new UserFriendlyException(LocalizedText.Format("Services.ToolChangeManagement.DuplicatedName", name));
        }

        if (await _toolChangeRepository.ExistsCodeAsync(code, cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            throw new UserFriendlyException(LocalizedText.Format("Services.ToolChangeManagement.DuplicatedCode", code));
        }

        var entity = new ToolChangeEntity
        {
            Name = name,
            Code = code
        };

        await _toolChangeRepository.AddAsync(entity, cancellationToken).ConfigureAwait(false);
        await _toolChangeRepository.UnitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Map(entity);
    }

    public async Task<ToolChangeDefinition> UpdateAsync(ToolChangeDefinition definition, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);
        GetRequiredCurrentUserId();

        if (definition.Id == Guid.Empty)
        {
            throw new UserFriendlyException(LocalizedText.Get("Services.ToolChangeManagement.IdRequired"));
        }

        var entity = await _toolChangeRepository.GetByIdAsync(definition.Id, cancellationToken).ConfigureAwait(false)
            ?? throw new EntityNotFoundException(LocalizedText.Format("Services.ToolChangeManagement.NotFoundById", definition.Id));

        var name = NormalizeRequired(definition.Name, LocalizedText.Get("Services.ToolChangeManagement.NameRequired"));
        var code = NormalizeRequired(definition.Code, LocalizedText.Get("Services.ToolChangeManagement.CodeRequired"));

        if (await _toolChangeRepository.ExistsNameAsync(name, entity.Id, cancellationToken).ConfigureAwait(false))
        {
            throw new UserFriendlyException(LocalizedText.Format("Services.ToolChangeManagement.DuplicatedName", name));
        }

        if (await _toolChangeRepository.ExistsCodeAsync(code, entity.Id, cancellationToken).ConfigureAwait(false))
        {
            throw new UserFriendlyException(LocalizedText.Format("Services.ToolChangeManagement.DuplicatedCode", code));
        }

        entity.Name = name;
        entity.Code = code;

        await _toolChangeRepository.UpdateAsync(entity, cancellationToken).ConfigureAwait(false);
        await _toolChangeRepository.UnitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Map(entity);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        GetRequiredCurrentUserId();
        if (id == Guid.Empty)
        {
            return;
        }

        await _toolChangeRepository.DeleteAsync(id, cancellationToken).ConfigureAwait(false);
        await _toolChangeRepository.UnitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static ToolChangeDefinition Map(ToolChangeEntity entity)
    {
        return new ToolChangeDefinition
        {
            Id = entity.Id,
            Name = entity.Name,
            Code = entity.Code,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedBy = entity.UpdatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt
        };
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