namespace WearPartsControl.ApplicationServices.UserConfig;

public interface IUserConfigService
{
    ValueTask<UserConfig> GetAsync(CancellationToken cancellationToken = default);

    ValueTask SaveAsync(UserConfig config, CancellationToken cancellationToken = default);
}