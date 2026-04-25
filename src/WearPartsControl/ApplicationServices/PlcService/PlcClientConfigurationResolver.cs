using Microsoft.Extensions.DependencyInjection;
using WearPartsControl.ApplicationServices.AppSettings;
using WearPartsControl.ApplicationServices.ClientAppInfo;

namespace WearPartsControl.ApplicationServices.PlcService;

public sealed class PlcClientConfigurationResolver : IPlcClientConfigurationResolver
{
    private readonly IAppSettingsService _appSettingsService;
    private readonly IServiceScopeFactory _serviceScopeFactory;

    public PlcClientConfigurationResolver(IAppSettingsService appSettingsService, IServiceScopeFactory serviceScopeFactory)
    {
        _appSettingsService = appSettingsService;
        _serviceScopeFactory = serviceScopeFactory;
    }

    public async Task<PlcClientConfigurationResolution> ResolveAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _appSettingsService.GetAsync(cancellationToken).ConfigureAwait(false);
        var resourceNumber = settings.ResourceNumber?.Trim() ?? string.Empty;
        if (!settings.IsSetClientAppInfo || string.IsNullOrWhiteSpace(resourceNumber))
        {
            return PlcClientConfigurationResolution.NotConfigured(resourceNumber);
        }

        await using var scope = _serviceScopeFactory.CreateAsyncScope();
        var clientAppInfoService = scope.ServiceProvider.GetRequiredService<IClientAppInfoService>();
        var clientAppInfo = await clientAppInfoService.GetAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(clientAppInfo.ResourceNumber)
            || string.IsNullOrWhiteSpace(clientAppInfo.PlcProtocolType)
            || string.IsNullOrWhiteSpace(clientAppInfo.PlcIpAddress))
        {
            return PlcClientConfigurationResolution.NotConfigured(resourceNumber);
        }

        return PlcClientConfigurationResolution.Configured(resourceNumber, clientAppInfo);
    }
}