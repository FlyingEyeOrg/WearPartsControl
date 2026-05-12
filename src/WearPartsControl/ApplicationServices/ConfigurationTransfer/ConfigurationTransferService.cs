using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text.Json;
using WearPartsControl.ApplicationServices.AppSettings;
using WearPartsControl.ApplicationServices.Localization;
using WearPartsControl.Exceptions;

namespace WearPartsControl.ApplicationServices.ConfigurationTransfer;

public sealed class ConfigurationTransferService : IConfigurationTransferService
{
    private const int CurrentFormatVersion = 1;
    private const string ManifestEntryName = "configuration-package.json";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = false,
        WriteIndented = true
    };

    private static readonly string[] IncludedRootNames = ["Settings", "LocalDB"];
    private static readonly HashSet<string> IncludedRootNameSet = new(IncludedRootNames, StringComparer.OrdinalIgnoreCase);

    private readonly IAppSettingsService _appSettingsService;
    private readonly string _rootDirectory;

    public ConfigurationTransferService(IAppSettingsService appSettingsService, string? rootDirectory = null)
    {
        _appSettingsService = appSettingsService;
        _rootDirectory = Path.GetFullPath(rootDirectory ?? PortableDataPaths.RootDirectory);
        Directory.CreateDirectory(_rootDirectory);
    }

    public async Task<ConfigurationTransferSummary> ExportAsync(string packagePath, CancellationToken cancellationToken = default)
    {
        packagePath = NormalizePackagePath(packagePath);
        Directory.CreateDirectory(Path.GetDirectoryName(packagePath) ?? Environment.CurrentDirectory);

        var tempPath = packagePath + ".tmp";
        if (File.Exists(tempPath))
        {
            File.Delete(tempPath);
        }

        var fileCount = 0;
        await using (var output = new FileStream(tempPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 64 * 1024, FileOptions.WriteThrough))
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: false))
        {
            var manifest = new ConfigurationPackageManifest(
                CurrentFormatVersion,
                "WearPartsControl",
                ResolveProductVersion(),
                DateTimeOffset.UtcNow,
                IncludedRootNames);

            var manifestEntry = archive.CreateEntry(ManifestEntryName, CompressionLevel.Optimal);
            await using (var manifestStream = manifestEntry.Open())
            {
                await JsonSerializer.SerializeAsync(manifestStream, manifest, SerializerOptions, cancellationToken).ConfigureAwait(false);
            }

            foreach (var rootName in IncludedRootNames)
            {
                var sourceRoot = Path.Combine(_rootDirectory, rootName);
                if (!Directory.Exists(sourceRoot))
                {
                    continue;
                }

                foreach (var filePath in Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (ShouldSkipFile(filePath, packagePath, tempPath))
                    {
                        continue;
                    }

                    var relativePath = Path.GetRelativePath(sourceRoot, filePath).Replace(Path.DirectorySeparatorChar, '/');
                    var entryName = rootName + "/" + relativePath.Replace(Path.AltDirectorySeparatorChar, '/');
                    var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);

                    await using var entryStream = entry.Open();
                    await using var input = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 64 * 1024, FileOptions.SequentialScan);
                    await input.CopyToAsync(entryStream, cancellationToken).ConfigureAwait(false);
                    fileCount++;
                }
            }
        }

        File.Move(tempPath, packagePath, overwrite: true);
        return new ConfigurationTransferSummary(packagePath, fileCount);
    }

    public async Task<ConfigurationTransferSummary> ImportAsync(string packagePath, CancellationToken cancellationToken = default)
    {
        packagePath = NormalizePackagePath(packagePath);
        if (!File.Exists(packagePath))
        {
            throw new UserFriendlyException(LocalizedText.Format("Services.ConfigurationTransfer.PackageNotFound", packagePath));
        }

        var currentSettings = await _appSettingsService.GetAsync(cancellationToken).ConfigureAwait(false);
        if (currentSettings.IsSetClientAppInfo || !string.IsNullOrWhiteSpace(currentSettings.ResourceNumber))
        {
            throw new UserFriendlyException(LocalizedText.Get("Services.ConfigurationTransfer.ImportRequiresUnconfiguredClient"));
        }

        var stagingDirectory = Path.Combine(Path.GetTempPath(), "WearPartsControl-ConfigImport-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stagingDirectory);

        try
        {
            var fileCount = await ExtractToStagingDirectoryAsync(packagePath, stagingDirectory, cancellationToken).ConfigureAwait(false);
            foreach (var rootName in IncludedRootNames)
            {
                var sourceRoot = Path.Combine(stagingDirectory, rootName);
                if (!Directory.Exists(sourceRoot))
                {
                    continue;
                }

                var destinationRoot = Path.Combine(_rootDirectory, rootName);
                ReplaceDirectory(sourceRoot, destinationRoot);
            }

            var importedSettings = await _appSettingsService.GetAsync(cancellationToken).ConfigureAwait(false);
            await _appSettingsService.SaveAsync(importedSettings, cancellationToken).ConfigureAwait(false);

            return new ConfigurationTransferSummary(packagePath, fileCount);
        }
        finally
        {
            if (Directory.Exists(stagingDirectory))
            {
                Directory.Delete(stagingDirectory, recursive: true);
            }
        }
    }

    private static string NormalizePackagePath(string packagePath)
    {
        if (string.IsNullOrWhiteSpace(packagePath))
        {
            throw new UserFriendlyException(LocalizedText.Get("Services.ConfigurationTransfer.PackagePathRequired"));
        }

        packagePath = Path.GetFullPath(packagePath.Trim());
        if (!string.Equals(Path.GetExtension(packagePath), ".cfg", StringComparison.OrdinalIgnoreCase))
        {
            throw new UserFriendlyException(LocalizedText.Get("Services.ConfigurationTransfer.PackageExtensionInvalid"));
        }

        return packagePath;
    }

    private static bool ShouldSkipFile(string filePath, string packagePath, string tempPath)
    {
        var fullPath = Path.GetFullPath(filePath);
        if (string.Equals(fullPath, packagePath, StringComparison.OrdinalIgnoreCase)
            || string.Equals(fullPath, tempPath, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var fileName = Path.GetFileName(filePath);
        return fileName.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase)
            || fileName.EndsWith(".log", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<int> ExtractToStagingDirectoryAsync(string packagePath, string stagingDirectory, CancellationToken cancellationToken)
    {
        var stagingRoot = EnsureTrailingDirectorySeparator(Path.GetFullPath(stagingDirectory));
        using var archive = ZipFile.OpenRead(packagePath);
        var manifestEntry = archive.GetEntry(ManifestEntryName)
            ?? throw new UserFriendlyException(LocalizedText.Get("Services.ConfigurationTransfer.ManifestMissing"));

        await using (var manifestStream = manifestEntry.Open())
        {
            var manifest = await JsonSerializer.DeserializeAsync<ConfigurationPackageManifest>(manifestStream, SerializerOptions, cancellationToken).ConfigureAwait(false)
                ?? throw new UserFriendlyException(LocalizedText.Get("Services.ConfigurationTransfer.ManifestInvalid"));

            if (manifest.FormatVersion != CurrentFormatVersion)
            {
                throw new UserFriendlyException(LocalizedText.Format("Services.ConfigurationTransfer.UnsupportedFormatVersion", manifest.FormatVersion));
            }

            if (!string.Equals(manifest.ProductName, "WearPartsControl", StringComparison.Ordinal))
            {
                throw new UserFriendlyException(LocalizedText.Get("Services.ConfigurationTransfer.ProductMismatch"));
            }
        }

        var fileCount = 0;
        foreach (var entry in archive.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.Equals(entry.FullName, ManifestEntryName, StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(entry.Name))
            {
                continue;
            }

            var normalizedEntryName = NormalizeEntryName(entry.FullName);
            var destinationPath = Path.GetFullPath(Path.Combine(stagingDirectory, normalizedEntryName));
            if (!destinationPath.StartsWith(stagingRoot, StringComparison.OrdinalIgnoreCase))
            {
                throw new UserFriendlyException(LocalizedText.Get("Services.ConfigurationTransfer.PackagePathInvalid"));
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath) ?? stagingDirectory);
            entry.ExtractToFile(destinationPath, overwrite: true);
            fileCount++;
        }

        return fileCount;
    }

    private static string NormalizeEntryName(string entryName)
    {
        var normalized = entryName.Replace('\\', '/').Trim('/');
        if (string.IsNullOrWhiteSpace(normalized)
            || normalized.Contains("..", StringComparison.Ordinal)
            || Path.IsPathRooted(normalized))
        {
            throw new UserFriendlyException(LocalizedText.Get("Services.ConfigurationTransfer.PackagePathInvalid"));
        }

        var firstSeparatorIndex = normalized.IndexOf('/', StringComparison.Ordinal);
        if (firstSeparatorIndex <= 0 || firstSeparatorIndex == normalized.Length - 1)
        {
            throw new UserFriendlyException(LocalizedText.Get("Services.ConfigurationTransfer.PackagePathInvalid"));
        }

        var rootName = firstSeparatorIndex < 0 ? normalized : normalized[..firstSeparatorIndex];
        var canonicalRootName = IncludedRootNames.FirstOrDefault(includedRootName => string.Equals(includedRootName, rootName, StringComparison.OrdinalIgnoreCase));
        if (canonicalRootName is null)
        {
            throw new UserFriendlyException(LocalizedText.Format("Services.ConfigurationTransfer.PackageRootInvalid", rootName));
        }

        var relativePath = normalized[(firstSeparatorIndex + 1)..];
        return Path.Combine(canonicalRootName, relativePath.Replace('/', Path.DirectorySeparatorChar));
    }

    private static string EnsureTrailingDirectorySeparator(string path)
    {
        return path.EndsWith(Path.DirectorySeparatorChar)
            ? path
            : path + Path.DirectorySeparatorChar;
    }

    private static void ReplaceDirectory(string sourceRoot, string destinationRoot)
    {
        Directory.CreateDirectory(destinationRoot);

        foreach (var filePath in Directory.EnumerateFiles(destinationRoot, "*", SearchOption.AllDirectories))
        {
            File.Delete(filePath);
        }

        foreach (var directoryPath in Directory.EnumerateDirectories(destinationRoot, "*", SearchOption.AllDirectories)
                     .OrderByDescending(static path => path.Length))
        {
            if (!Directory.EnumerateFileSystemEntries(directoryPath).Any())
            {
                Directory.Delete(directoryPath);
            }
        }

        foreach (var sourceFilePath in Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceRoot, sourceFilePath);
            var destinationPath = Path.Combine(destinationRoot, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath) ?? destinationRoot);
            File.Copy(sourceFilePath, destinationPath, overwrite: true);
        }
    }

    private static string ResolveProductVersion()
    {
        return typeof(ConfigurationTransferService).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? typeof(ConfigurationTransferService).Assembly.GetName().Version?.ToString()
            ?? string.Empty;
    }

    private sealed record ConfigurationPackageManifest(
        int FormatVersion,
        string ProductName,
        string ProductVersion,
        DateTimeOffset ExportedAt,
        IReadOnlyList<string> IncludedRoots);
}