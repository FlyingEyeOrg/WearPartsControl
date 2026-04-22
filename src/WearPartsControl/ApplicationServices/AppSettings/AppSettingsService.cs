using System.IO;
using System.Text.Json;
using WearPartsControl.ApplicationServices.SaveInfoService;

namespace WearPartsControl.ApplicationServices.AppSettings;

public sealed class AppSettingsService : IAppSettingsService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = false,
        WriteIndented = false
    };

    private readonly ISaveInfoStore _saveInfoStore;
    private readonly string _settingsDirectory;
    private readonly string _legacyFilePath;
    private readonly string _currentFilePath;

    public event EventHandler<AppSettings>? SettingsSaved;

    public AppSettingsService(ISaveInfoStore saveInfoStore, string? settingsDirectory = null)
    {
        _saveInfoStore = saveInfoStore;
        _settingsDirectory = Path.GetFullPath(settingsDirectory ?? PortableDataPaths.SettingsDirectory);
        _legacyFilePath = Path.Combine(_settingsDirectory, "app-setting.json");
        _currentFilePath = Path.Combine(_settingsDirectory, "app-settings.json");
    }

    public async ValueTask<AppSettings> GetAsync(CancellationToken cancellationToken = default)
    {
        await MigrateLegacyFileIfNeededAsync(cancellationToken).ConfigureAwait(false);

        var settings = await _saveInfoStore.ReadAsync<AppSettings>(cancellationToken).ConfigureAwait(false);
        return Normalize(settings);
    }

    public ValueTask SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var normalized = Normalize(settings);
        var task = _saveInfoStore.WriteAsync(normalized, cancellationToken);
        return NotifyAfterSaveAsync(task, normalized);
    }

    private async Task MigrateLegacyFileIfNeededAsync(CancellationToken cancellationToken)
    {
        if (File.Exists(_currentFilePath) || !File.Exists(_legacyFilePath))
        {
            return;
        }

        Directory.CreateDirectory(_settingsDirectory);

        AppSettings legacySettings;
        await using (var stream = new FileStream(_legacyFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 16 * 1024, FileOptions.SequentialScan))
        {
            legacySettings = await JsonSerializer.DeserializeAsync<AppSettings>(stream, SerializerOptions, cancellationToken).ConfigureAwait(false)
                ?? new AppSettings();
        }

        await _saveInfoStore.WriteAsync(Normalize(legacySettings), cancellationToken).ConfigureAwait(false);
        File.Delete(_legacyFilePath);
    }

    private static AppSettings Normalize(AppSettings settings)
    {
        var plcPipeline = settings.PlcPipeline ?? new PlcPipelineSettings();

        return new AppSettings
        {
            ResourceNumber = settings.ResourceNumber?.Trim() ?? string.Empty,
            LoginInputMaxIntervalMilliseconds = settings.LoginInputMaxIntervalMilliseconds <= 0
                ? 80
                : settings.LoginInputMaxIntervalMilliseconds,
            AutoLogoutCountdownSeconds = settings.AutoLogoutCountdownSeconds <= 0
                ? 360
                : settings.AutoLogoutCountdownSeconds,
            UseWorkNumberLogin = settings.UseWorkNumberLogin,
            PlcPipeline = new PlcPipelineSettings
            {
                SlowQueueWaitThresholdMilliseconds = plcPipeline.SlowQueueWaitThresholdMilliseconds <= 0
                    ? 100
                    : plcPipeline.SlowQueueWaitThresholdMilliseconds,
                SlowExecutionThresholdMilliseconds = plcPipeline.SlowExecutionThresholdMilliseconds <= 0
                    ? 500
                    : plcPipeline.SlowExecutionThresholdMilliseconds
            },
            IsSetClientAppInfo = settings.IsSetClientAppInfo
        };
    }

    private async ValueTask NotifyAfterSaveAsync(ValueTask writeTask, AppSettings normalized)
    {
        await writeTask.ConfigureAwait(false);
        SettingsSaved?.Invoke(this, Clone(normalized));
    }

    private static AppSettings Clone(AppSettings settings)
    {
        return new AppSettings
        {
            ResourceNumber = settings.ResourceNumber,
            LoginInputMaxIntervalMilliseconds = settings.LoginInputMaxIntervalMilliseconds,
            AutoLogoutCountdownSeconds = settings.AutoLogoutCountdownSeconds,
            UseWorkNumberLogin = settings.UseWorkNumberLogin,
            PlcPipeline = new PlcPipelineSettings
            {
                SlowQueueWaitThresholdMilliseconds = settings.PlcPipeline.SlowQueueWaitThresholdMilliseconds,
                SlowExecutionThresholdMilliseconds = settings.PlcPipeline.SlowExecutionThresholdMilliseconds
            },
            IsSetClientAppInfo = settings.IsSetClientAppInfo
        };
    }
}