using WearPartsControl.ApplicationServices.Localization;
using Microsoft.Extensions.Logging;

namespace WearPartsControl.ApplicationServices.PlcService;

public sealed class PlcStartupConnectionService : IPlcStartupConnectionService
{
    private readonly IPlcClientConfigurationResolver _plcClientConfigurationResolver;
    private readonly IPlcOperationPipeline _plcOperationPipeline;
    private readonly IPlcConnectionStatusService _plcConnectionStatusService;
    private readonly ILogger<PlcStartupConnectionService> _logger;

    public PlcStartupConnectionService(
        IPlcClientConfigurationResolver plcClientConfigurationResolver,
        IPlcOperationPipeline plcOperationPipeline,
        IPlcConnectionStatusService plcConnectionStatusService,
        ILogger<PlcStartupConnectionService> logger)
    {
        _plcClientConfigurationResolver = plcClientConfigurationResolver;
        _plcOperationPipeline = plcOperationPipeline;
        _plcConnectionStatusService = plcConnectionStatusService;
        _logger = logger;
    }

    public async Task<PlcStartupConnectionResult> EnsureConnectedAsync(CancellationToken cancellationToken = default)
    {
        var configurationResolution = await _plcClientConfigurationResolver.ResolveAsync(cancellationToken).ConfigureAwait(false);
        if (!configurationResolution.IsConfigured)
        {
            var notConfigured = PlcStartupConnectionResult.NotConfigured();
            _plcConnectionStatusService.Set(notConfigured);
            return notConfigured;
        }

        var clientAppInfo = configurationResolution.ClientAppInfo!;

        try
        {
            _plcConnectionStatusService.Set(PlcStartupConnectionResult.Connecting());
            var connectionOptions = PlcConnectionOptionsFactory.Create(clientAppInfo);
            await _plcOperationPipeline.ConnectAsync(PlcStartupPipelineOperations.EnsureConnected, connectionOptions, cancellationToken).ConfigureAwait(false);
            var connected = PlcStartupConnectionResult.Connected();
            _plcConnectionStatusService.Set(connected);
            return connected;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, LocalizedText.Get("Services.PlcStartupConnection.LogConnectFailed"), clientAppInfo.ResourceNumber);
            var failed = PlcStartupConnectionResult.Failed(LocalizedText.Format("Services.PlcStartupConnection.ConnectFailed", ex.Message));
            _plcConnectionStatusService.Set(failed);
            return failed;
        }
    }
}