using System.Globalization;
using System.Threading;
using WearPartsControl.ApplicationServices.Localization;
using WearPartsControl.ApplicationServices.SaveInfoService;
using WearPartsControl.Exceptions;

namespace WearPartsControl.ApplicationServices.PartServices;

public sealed class KdlRecipeManagementService : ApplicationServiceBase, IKdlRecipeManagementService
{
    private readonly ISaveInfoStore _saveInfoStore;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public KdlRecipeManagementService(ICurrentUserAccessor currentUserAccessor, ISaveInfoStore saveInfoStore)
        : base(currentUserAccessor)
    {
        _saveInfoStore = saveInfoStore;
    }

    public async ValueTask<KdlRecipeSettingsState> GetAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var document = await ReadDocumentAsync(cancellationToken).ConfigureAwait(false);
            return MapState(document);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask<KdlRecipeDefinition?> GetCurrentRecipeAsync(CancellationToken cancellationToken = default)
    {
        var state = await GetAsync(cancellationToken).ConfigureAwait(false);
        return state.Recipes.FirstOrDefault(recipe => recipe.IsCurrent);
    }

    public async ValueTask<KdlRecipeDefinition> CreateAsync(KdlRecipeDefinition definition, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var currentUserId = GetRequiredCurrentUserId();
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var document = await ReadDocumentAsync(cancellationToken).ConfigureAwait(false);
            var name = NormalizeRequiredName(definition.Name);
            ValidateLimits(definition.LowerLimit, definition.UpperLimit);
            EnsureUniqueName(document.Recipes, name, null);

            var now = DateTime.UtcNow;
            var record = new KdlRecipeRecord
            {
                Id = definition.Id == Guid.Empty ? Guid.NewGuid() : definition.Id,
                Name = name,
                LowerLimit = definition.LowerLimit,
                UpperLimit = definition.UpperLimit,
                CreatedAt = now,
                UpdatedAt = now,
                CreatedBy = currentUserId,
                UpdatedBy = currentUserId
            };

            document.Recipes.Add(record);
            if (!document.CurrentRecipeId.HasValue)
            {
                document.CurrentRecipeId = record.Id;
            }

            await WriteDocumentAsync(document, cancellationToken).ConfigureAwait(false);
            return Map(record, document.CurrentRecipeId);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask<KdlRecipeDefinition> UpdateAsync(KdlRecipeDefinition definition, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var currentUserId = GetRequiredCurrentUserId();
        if (definition.Id == Guid.Empty)
        {
            throw new UserFriendlyException(LocalizedText.Get("Services.KdlRecipeManagement.IdRequired"));
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var document = await ReadDocumentAsync(cancellationToken).ConfigureAwait(false);
            var record = document.Recipes.FirstOrDefault(item => item.Id == definition.Id)
                ?? throw new EntityNotFoundException(LocalizedText.Format("Services.KdlRecipeManagement.NotFoundById", definition.Id));

            var name = NormalizeRequiredName(definition.Name);
            ValidateLimits(definition.LowerLimit, definition.UpperLimit);
            EnsureUniqueName(document.Recipes, name, definition.Id);

            record.Name = name;
            record.LowerLimit = definition.LowerLimit;
            record.UpperLimit = definition.UpperLimit;
            record.UpdatedAt = DateTime.UtcNow;
            record.UpdatedBy = currentUserId;

            await WriteDocumentAsync(document, cancellationToken).ConfigureAwait(false);
            return Map(record, document.CurrentRecipeId);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask SetCurrentAsync(Guid recipeId, CancellationToken cancellationToken = default)
    {
        var currentUserId = GetRequiredCurrentUserId();
        if (recipeId == Guid.Empty)
        {
            throw new UserFriendlyException(LocalizedText.Get("Services.KdlRecipeManagement.IdRequired"));
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var document = await ReadDocumentAsync(cancellationToken).ConfigureAwait(false);
            var record = document.Recipes.FirstOrDefault(item => item.Id == recipeId)
                ?? throw new EntityNotFoundException(LocalizedText.Format("Services.KdlRecipeManagement.NotFoundById", recipeId));

            document.CurrentRecipeId = record.Id;
            record.UpdatedAt = DateTime.UtcNow;
            record.UpdatedBy = currentUserId;

            await WriteDocumentAsync(document, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DeleteAsync(Guid recipeId, CancellationToken cancellationToken = default)
    {
        GetRequiredCurrentUserId();
        if (recipeId == Guid.Empty)
        {
            return;
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var document = await ReadDocumentAsync(cancellationToken).ConfigureAwait(false);
            var removedCount = document.Recipes.RemoveAll(item => item.Id == recipeId);
            if (removedCount == 0)
            {
                return;
            }

            if (document.CurrentRecipeId == recipeId)
            {
                document.CurrentRecipeId = document.Recipes
                    .OrderByDescending(item => item.CreatedAt ?? DateTime.MinValue)
                    .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(item => (Guid?)item.Id)
                    .FirstOrDefault();
            }

            await WriteDocumentAsync(document, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async ValueTask<KdlRecipeSettingsDocument> ReadDocumentAsync(CancellationToken cancellationToken)
    {
        var document = await _saveInfoStore.ReadAsync<KdlRecipeSettingsDocument>(cancellationToken).ConfigureAwait(false);
        return NormalizeDocument(document);
    }

    private ValueTask WriteDocumentAsync(KdlRecipeSettingsDocument document, CancellationToken cancellationToken)
    {
        return _saveInfoStore.WriteAsync(NormalizeDocument(document), cancellationToken);
    }

    private static KdlRecipeSettingsDocument NormalizeDocument(KdlRecipeSettingsDocument document)
    {
        document ??= new KdlRecipeSettingsDocument();

        var recipes = document.Recipes ?? [];
        foreach (var recipe in recipes)
        {
            recipe.Id = recipe.Id == Guid.Empty ? Guid.NewGuid() : recipe.Id;
            recipe.Name = recipe.Name?.Trim() ?? string.Empty;
            recipe.CreatedAt ??= DateTime.UtcNow;
            recipe.UpdatedAt ??= recipe.CreatedAt;
            recipe.CreatedBy ??= string.Empty;
            recipe.UpdatedBy ??= recipe.CreatedBy;
        }

        document.Recipes = recipes
            .OrderByDescending(item => item.CreatedAt ?? DateTime.MinValue)
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (document.CurrentRecipeId.HasValue && document.Recipes.All(item => item.Id != document.CurrentRecipeId.Value))
        {
            document.CurrentRecipeId = null;
        }

        return document;
    }

    private static KdlRecipeSettingsState MapState(KdlRecipeSettingsDocument document)
    {
        return new KdlRecipeSettingsState
        {
            CurrentRecipeId = document.CurrentRecipeId,
            Recipes = document.Recipes.Select(item => Map(item, document.CurrentRecipeId)).ToArray()
        };
    }

    private static KdlRecipeDefinition Map(KdlRecipeRecord record, Guid? currentRecipeId)
    {
        return new KdlRecipeDefinition
        {
            Id = record.Id,
            Name = record.Name,
            LowerLimit = record.LowerLimit,
            UpperLimit = record.UpperLimit,
            IsCurrent = currentRecipeId.HasValue && record.Id == currentRecipeId.Value,
            CreatedAt = record.CreatedAt,
            CreatedBy = record.CreatedBy,
            UpdatedBy = record.UpdatedBy,
            UpdatedAt = record.UpdatedAt
        };
    }

    private static string NormalizeRequiredName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new UserFriendlyException(LocalizedText.Get("Services.KdlRecipeManagement.NameRequired"));
        }

        return value.Trim();
    }

    private static void EnsureUniqueName(IEnumerable<KdlRecipeRecord> recipes, string name, Guid? excludeId)
    {
        if (recipes.Any(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase) && (!excludeId.HasValue || item.Id != excludeId.Value)))
        {
            throw new UserFriendlyException(LocalizedText.Format("Services.KdlRecipeManagement.DuplicatedName", name));
        }
    }

    private static void ValidateLimits(double lowerLimit, double upperLimit)
    {
        if (double.IsNaN(lowerLimit) || double.IsInfinity(lowerLimit))
        {
            throw new UserFriendlyException(LocalizedText.Get("Services.KdlRecipeManagement.LowerLimitInvalid"));
        }

        if (double.IsNaN(upperLimit) || double.IsInfinity(upperLimit))
        {
            throw new UserFriendlyException(LocalizedText.Get("Services.KdlRecipeManagement.UpperLimitInvalid"));
        }

        if (lowerLimit > upperLimit)
        {
            throw new UserFriendlyException(LocalizedText.Format(
                "Services.KdlRecipeManagement.RangeInvalid",
                lowerLimit.ToString(CultureInfo.InvariantCulture),
                upperLimit.ToString(CultureInfo.InvariantCulture)));
        }
    }
}