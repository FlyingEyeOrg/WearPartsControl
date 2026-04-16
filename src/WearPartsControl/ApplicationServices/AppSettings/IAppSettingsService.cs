namespace WearPartsControl.ApplicationServices.AppSettings;

public interface IAppSettingsService
{
    ValueTask<AppSettings> GetAsync(CancellationToken cancellationToken = default);

    ValueTask SaveAsync(AppSettings settings, CancellationToken cancellationToken = default);
}