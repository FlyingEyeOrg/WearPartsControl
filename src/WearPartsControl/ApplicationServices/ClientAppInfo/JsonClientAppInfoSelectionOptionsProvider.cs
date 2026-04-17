using System.IO;
using System.Text.Json;
using WearPartsControl.ApplicationServices.Localization;

namespace WearPartsControl.ApplicationServices.ClientAppInfo;

public sealed class JsonClientAppInfoSelectionOptionsProvider : IClientAppInfoSelectionOptionsProvider
{
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
        var path = ResolveConfigurationPath();
        if (!File.Exists(path))
        {
            return new ClientAppInfoSelectionOptions();
        }

        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 16 * 1024, FileOptions.SequentialScan);
        var document = await JsonSerializer.DeserializeAsync<ClientAppInfoSelectionOptionsDocument>(stream, SerializerOptions, cancellationToken).ConfigureAwait(false)
            ?? new ClientAppInfoSelectionOptionsDocument();

        return new ClientAppInfoSelectionOptions
        {
            AreaOptions = Normalize(document.AreaOptions),
            ProcedureOptions = Normalize(document.ProcedureOptions)
        };
    }

    private string ResolveConfigurationPath()
    {
        foreach (var cultureName in GetCandidateCultureNames(_localizationService.CurrentCulture))
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
