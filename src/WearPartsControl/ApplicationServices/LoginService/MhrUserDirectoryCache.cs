using System.IO;
using System.Text.Json;

namespace WearPartsControl.ApplicationServices.LoginService;

public sealed class MhrUserDirectoryCache : IMhrUserDirectoryCache
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = false,
        PropertyNameCaseInsensitive = true
    };

    private readonly SemaphoreSlim _syncRoot = new(1, 1);
    private readonly string _cachePath;
    private MhrUserDirectoryCacheDocument? _document;

    public MhrUserDirectoryCache(string? cachePath = null)
    {
        _cachePath = Path.GetFullPath(cachePath ?? Path.Combine(PortableDataPaths.SettingsDirectory, "mhr-user-cache.json"));
    }

    public async Task<MhrUser?> FindUserAsync(
        string site,
        string resourceId,
        string authId,
        bool isIdCard,
        int cacheDays,
        CancellationToken cancellationToken = default)
    {
        var normalizedSite = NormalizeRequired(site);
        var normalizedResourceId = NormalizeRequired(resourceId);
        var normalizedAuthId = NormalizeRequired(authId);
        var effectiveCacheDays = cacheDays <= 0 ? 1 : cacheDays;

        await _syncRoot.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var document = await LoadDocumentCoreAsync(cancellationToken).ConfigureAwait(false);
            var entry = document.Entries.FirstOrDefault(x =>
                string.Equals(x.Site, normalizedSite, StringComparison.OrdinalIgnoreCase)
                && string.Equals(x.ResourceId, normalizedResourceId, StringComparison.OrdinalIgnoreCase));

            if (entry is null || entry.FetchedAt.AddDays(effectiveCacheDays) <= DateTime.UtcNow)
            {
                return null;
            }

            var user = entry.Users.FirstOrDefault(x => isIdCard
                ? string.Equals(x.CardId, normalizedAuthId, StringComparison.Ordinal)
                : string.Equals(x.WorkId, normalizedAuthId, StringComparison.Ordinal));

            return Clone(user);
        }
        finally
        {
            _syncRoot.Release();
        }
    }

    public async Task SaveUsersAsync(
        string site,
        string resourceId,
        IReadOnlyCollection<MhrUser> users,
        DateTime fetchedAt,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(users);

        var normalizedSite = NormalizeRequired(site);
        var normalizedResourceId = NormalizeRequired(resourceId);

        await _syncRoot.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var document = await LoadDocumentCoreAsync(cancellationToken).ConfigureAwait(false);
            var entry = document.Entries.FirstOrDefault(x =>
                string.Equals(x.Site, normalizedSite, StringComparison.OrdinalIgnoreCase)
                && string.Equals(x.ResourceId, normalizedResourceId, StringComparison.OrdinalIgnoreCase));

            if (entry is null)
            {
                entry = new MhrUserDirectoryCacheEntry();
                document.Entries.Add(entry);
            }

            entry.Site = normalizedSite;
            entry.ResourceId = normalizedResourceId;
            entry.FetchedAt = fetchedAt;
            entry.Users = users.Select(Clone).Where(x => x is not null).Cast<MhrUser>().ToList();

            Directory.CreateDirectory(Path.GetDirectoryName(_cachePath)!);
            await File.WriteAllTextAsync(_cachePath, JsonSerializer.Serialize(document, SerializerOptions), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _syncRoot.Release();
        }
    }

    private async Task<MhrUserDirectoryCacheDocument> LoadDocumentCoreAsync(CancellationToken cancellationToken)
    {
        if (_document is not null)
        {
            return _document;
        }

        if (!File.Exists(_cachePath))
        {
            _document = new MhrUserDirectoryCacheDocument();
            return _document;
        }

        var json = await File.ReadAllTextAsync(_cachePath, cancellationToken).ConfigureAwait(false);
        _document = JsonSerializer.Deserialize<MhrUserDirectoryCacheDocument>(json, SerializerOptions) ?? new MhrUserDirectoryCacheDocument();
        return _document;
    }

    private static string NormalizeRequired(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim();
    }

    private static MhrUser? Clone(MhrUser? user)
    {
        if (user is null)
        {
            return null;
        }

        return new MhrUser
        {
            CardId = user.CardId,
            WorkId = user.WorkId,
            AccessLevel = user.AccessLevel
        };
    }

    private sealed class MhrUserDirectoryCacheDocument
    {
        public List<MhrUserDirectoryCacheEntry> Entries { get; set; } = new();
    }

    private sealed class MhrUserDirectoryCacheEntry
    {
        public string Site { get; set; } = string.Empty;

        public string ResourceId { get; set; } = string.Empty;

        public DateTime FetchedAt { get; set; }

        public List<MhrUser> Users { get; set; } = new();
    }
}