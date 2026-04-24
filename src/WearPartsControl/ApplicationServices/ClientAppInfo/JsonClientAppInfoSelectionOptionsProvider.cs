using System.IO;
using System.Text.Json;
using WearPartsControl.ApplicationServices.Localization;

namespace WearPartsControl.ApplicationServices.ClientAppInfo;

public sealed class JsonClientAppInfoSelectionOptionsProvider : IClientAppInfoSelectionOptionsProvider
{
    private static readonly string[] SupportedCultures = ["zh-CN", "en-US"];
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = false
    };

    private readonly ILocalizationService _localizationService;

    public JsonClientAppInfoSelectionOptionsProvider(ILocalizationService localizationService)
    {
        _localizationService = localizationService;
    }

    public async Task<ClientAppInfoSelectionOptions> GetAsync(CancellationToken cancellationToken = default)
    {
        var document = await LoadDocumentAsync(_localizationService.CurrentCulture, cancellationToken).ConfigureAwait(false);
        if (document is null)
        {
            return new ClientAppInfoSelectionOptions();
        }

        return new ClientAppInfoSelectionOptions
        {
            AreaOptions = Normalize(document.AreaOptions),
            ProcedureOptions = Normalize(document.ProcedureOptions)
        };
    }

    public Task<string> MapAreaOptionAsync(string value, string targetCultureName, CancellationToken cancellationToken = default)
    {
        return MapOptionAsync(value, targetCultureName, static document => document.AreaOptions, cancellationToken);
    }

    public Task<string> MapProcedureOptionAsync(string value, string targetCultureName, CancellationToken cancellationToken = default)
    {
        return MapOptionAsync(value, targetCultureName, static document => document.ProcedureOptions, cancellationToken);
    }

    private async Task<string> MapOptionAsync(
        string value,
        string targetCultureName,
        Func<ClientAppInfoSelectionOptionsDocument, List<string>> selector,
        CancellationToken cancellationToken)
    {
        var normalizedValue = value?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedValue))
        {
            return string.Empty;
        }

        var targetCulture = System.Globalization.CultureInfo.GetCultureInfo(targetCultureName);
        var targetDocument = await LoadDocumentAsync(targetCulture, cancellationToken).ConfigureAwait(false);
        var targetOptions = Normalize(selector(targetDocument));
        var directMatch = targetOptions.FirstOrDefault(option => string.Equals(option, normalizedValue, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(directMatch))
        {
            return directMatch;
        }

        foreach (var cultureName in SupportedCultures)
        {
            var sourceDocument = await LoadDocumentAsync(System.Globalization.CultureInfo.GetCultureInfo(cultureName), cancellationToken).ConfigureAwait(false);
            var sourceOptions = Normalize(selector(sourceDocument));
            var matchIndex = Array.FindIndex(sourceOptions.ToArray(), option => string.Equals(option, normalizedValue, StringComparison.OrdinalIgnoreCase));
            if (matchIndex < 0 || matchIndex >= targetOptions.Count)
            {
                continue;
            }

            return targetOptions[matchIndex];
        }

        return normalizedValue;
    }

    private async Task<ClientAppInfoSelectionOptionsDocument> LoadDocumentAsync(System.Globalization.CultureInfo culture, CancellationToken cancellationToken)
    {
        var path = ResolveConfigurationPath(culture);
        if (!File.Exists(path))
        {
            return new ClientAppInfoSelectionOptionsDocument();
        }

        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 16 * 1024, FileOptions.SequentialScan);
        return await JsonSerializer.DeserializeAsync<ClientAppInfoSelectionOptionsDocument>(stream, SerializerOptions, cancellationToken).ConfigureAwait(false)
            ?? new ClientAppInfoSelectionOptionsDocument();
    }

    private string ResolveConfigurationPath(System.Globalization.CultureInfo culture)
    {
        foreach (var cultureName in GetCandidateCultureNames(culture))
        {
            var path = Path.Combine(PortableDataPaths.SettingsDirectory, $"client-app-info.{cultureName}.json");
            if (File.Exists(path))
            {
                return path;
            }
        }

        return Path.Combine(PortableDataPaths.SettingsDirectory, "client-app-info.zh-CN.json");
    }

    private static IEnumerable<string> GetCandidateCultureNames(System.Globalization.CultureInfo culture)
    {
        var current = culture;
        while (!string.IsNullOrWhiteSpace(current.Name))
        {
            yield return current.Name;
            current = current.Parent;
        }

        yield return "zh-CN";
    }

    private static IReadOnlyList<string> Normalize(IEnumerable<string>? values)
    {
        if (values is null)
        {
            return Array.Empty<string>();
        }

        return values
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private sealed class ClientAppInfoSelectionOptionsDocument
    {
        public List<string> AreaOptions { get; set; } = new();

        public List<string> ProcedureOptions { get; set; } = new();
    }
}
