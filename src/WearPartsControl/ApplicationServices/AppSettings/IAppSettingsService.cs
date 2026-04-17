namespace WearPartsControl.ApplicationServices.AppSettings;

public interface IAppSettingsService
{
    event EventHandler<AppSettings>? SettingsSaved;

    ValueTask<AppSettings> GetAsync(CancellationToken cancellationToken = default);

    ValueTask SaveAsync(AppSettings settings, CancellationToken cancellationToken = default);
}