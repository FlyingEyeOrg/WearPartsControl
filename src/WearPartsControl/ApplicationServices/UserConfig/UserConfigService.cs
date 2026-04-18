using WearPartsControl.ApplicationServices.SaveInfoService;

namespace WearPartsControl.ApplicationServices.UserConfig;

public sealed class UserConfigService : IUserConfigService
{
    private readonly ISaveInfoStore _saveInfoStore;

    public UserConfigService(ISaveInfoStore saveInfoStore)
    {
        _saveInfoStore = saveInfoStore;
    }

    public async ValueTask<UserConfig> GetAsync(CancellationToken cancellationToken = default)
    {
        var config = await _saveInfoStore.ReadAsync<UserConfig>(cancellationToken).ConfigureAwait(false);
        return Normalize(config);
    }

    public ValueTask SaveAsync(UserConfig config, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(config);
        return _saveInfoStore.WriteAsync(Normalize(config), cancellationToken);
    }

    private static UserConfig Normalize(UserConfig config)
    {
        return new UserConfig
        {
            MeResponsibleWorkId = config.MeResponsibleWorkId?.Trim() ?? string.Empty,
            PrdResponsibleWorkId = config.PrdResponsibleWorkId?.Trim() ?? string.Empty,
            ComAccessToken = config.ComAccessToken?.Trim() ?? string.Empty,
            ComSecret = config.ComSecret?.Trim() ?? string.Empty
        };
    }
}