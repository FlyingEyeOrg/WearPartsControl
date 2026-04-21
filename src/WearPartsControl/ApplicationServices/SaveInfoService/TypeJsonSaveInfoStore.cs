using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using WearPartsControl.ApplicationServices.Localization;

namespace WearPartsControl.ApplicationServices.SaveInfoService;

public sealed class TypeJsonSaveInfoStore : ISaveInfoStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = false,
        PropertyNameCaseInsensitive = false
    };

    private readonly string _rootDirectory;
    private readonly ConcurrentDictionary<Type, string> _mappedFilePathCache = new();
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _fileLocks = new(StringComparer.OrdinalIgnoreCase);

    public TypeJsonSaveInfoStore(string? rootDirectory = null)
    {
        _rootDirectory = rootDirectory ?? PortableDataPaths.SettingsDirectory;
        _rootDirectory = Path.GetFullPath(_rootDirectory);
        Directory.CreateDirectory(_rootDirectory);
    }

    public async ValueTask<T> ReadAsync<T>(CancellationToken cancellationToken = default) where T : class, new()
    {
        var path = GetMappedFilePath(typeof(T));
        var gate = _fileLocks.GetOrAdd(path, _ => new SemaphoreSlim(1, 1));

        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!File.Exists(path))
            {
                return new T();
            }

            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 16 * 1024, FileOptions.SequentialScan);
            if (stream.Length == 0)
            {
                return new T();
            }

            var model = await JsonSerializer.DeserializeAsync<T>(stream, SerializerOptions, cancellationToken).ConfigureAwait(false);
            return model ?? new T();
        }
        finally
        {
            gate.Release();
        }
    }

    public async ValueTask WriteAsync<T>(T model, CancellationToken cancellationToken = default) where T : class, new()
    {
        ArgumentNullException.ThrowIfNull(model);

        var path = GetMappedFilePath(typeof(T));
        var gate = _fileLocks.GetOrAdd(path, _ => new SemaphoreSlim(1, 1));

        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var directory = Path.GetDirectoryName(path) ?? _rootDirectory;
            Directory.CreateDirectory(directory);

            var tempPath = path + ".tmp";
            await using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 16 * 1024, FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(stream, model, SerializerOptions, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            if (File.Exists(path))
            {
                File.Move(tempPath, path, true);
            }
            else
            {
                File.Move(tempPath, path);
            }
        }
        finally
        {
            gate.Release();
        }
    }

    private string GetMappedFilePath(Type modelType)
    {
        return _mappedFilePathCache.GetOrAdd(modelType, ResolveFilePath);
    }

    private string ResolveFilePath(Type modelType)
    {
        var attr = modelType.GetCustomAttributes(typeof(SaveInfoFileAttribute), false)
            .OfType<SaveInfoFileAttribute>()
            .FirstOrDefault();

        var fileName = attr?.FileName;
        if (string.IsNullOrWhiteSpace(fileName))
        {
            fileName = modelType.FullName ?? modelType.Name;
        }

        if (!string.Equals(Path.GetExtension(fileName), ".json", StringComparison.OrdinalIgnoreCase))
        {
            fileName += ".json";
        }

        ValidateRelativePath(fileName, modelType);

        var baseDir = attr?.BaseDirectory;
        if (!string.IsNullOrWhiteSpace(baseDir))
        {
            ValidateRelativePath(baseDir, modelType);
        }

        var combined = string.IsNullOrWhiteSpace(baseDir)
            ? Path.Combine(_rootDirectory, fileName)
            : Path.Combine(_rootDirectory, baseDir, fileName);

        var fullPath = Path.GetFullPath(combined);
        if (!fullPath.StartsWith(_rootDirectory, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(LocalizedText.Format("Services.SaveInfo.PathEscaped", modelType.FullName ?? modelType.Name));
        }

        return fullPath;
    }

    private static void ValidateRelativePath(string value, Type modelType)
    {
        if (Path.IsPathRooted(value) || value.Contains("..", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(LocalizedText.Format("Services.SaveInfo.PathMustBeRelative", modelType.FullName ?? modelType.Name));
        }

        var invalidChars = Path.GetInvalidPathChars();
        if (value.Any(invalidChars.Contains))
        {
            throw new InvalidOperationException(LocalizedText.Format("Services.SaveInfo.PathContainsInvalidChars", modelType.FullName ?? modelType.Name));
        }
    }
}
