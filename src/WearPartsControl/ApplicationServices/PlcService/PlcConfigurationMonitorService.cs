using System.Threading;
using Microsoft.Extensions.Logging;
using WearPartsControl.ApplicationServices.AppSettings;
using WearPartsControl.ApplicationServices.ClientAppInfo;
using WearPartsControl.ApplicationServices.Localization;
using AppSettingsModel = WearPartsControl.ApplicationServices.AppSettings.AppSettings;

namespace WearPartsControl.ApplicationServices.PlcService;

public sealed class PlcConfigurationMonitorService : IDisposable
{
    private readonly object _stateLock = new();
    private readonly IAppSettingsService _appSettingsService;
    private readonly IPlcClientConfigurationResolver _plcClientConfigurationResolver;
    private readonly IPlcOperationPipeline _plcOperationPipeline;
    private readonly IPlcConnectionStatusService _plcConnectionStatusService;
    private readonly ILogger<PlcConfigurationMonitorService> _logger;
    private string? _lastAppliedConfigurationKey;
    private bool _refreshRequested;
    private int _isProcessing;
    private bool _isDisposed;

    public PlcConfigurationMonitorService(
        IAppSettingsService appSettingsService,
        IPlcClientConfigurationResolver plcClientConfigurationResolver,
        IPlcOperationPipeline plcOperationPipeline,
        IPlcConnectionStatusService plcConnectionStatusService,
        ILogger<PlcConfigurationMonitorService> logger)
    {
        _appSettingsService = appSettingsService;
        _plcClientConfigurationResolver = plcClientConfigurationResolver;
        _plcOperationPipeline = plcOperationPipeline;
        _plcConnectionStatusService = plcConnectionStatusService;
        _logger = logger;

        _appSettingsService.SettingsSaved += OnSettingsSaved;
    }

    public void Dispose()
    {
        lock (_stateLock)
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
        }

        _appSettingsService.SettingsSaved -= OnSettingsSaved;
    }

    private void OnSettingsSaved(object? sender, AppSettingsModel settings)
    {
        lock (_stateLock)
        {
            if (_isDisposed)
            {
                return;
            }

            _refreshRequested = true;
        }

        _ = ProcessPendingRefreshesAsync();
    }

    private async Task ProcessPendingRefreshesAsync()
    {
        if (Interlocked.CompareExchange(ref _isProcessing, 1, 0) != 0)
        {
            return;
        }

        try
        {
            while (TryConsumeRefreshRequest())
            {
                await RefreshConnectionAsync(CancellationToken.None).ConfigureAwait(false);
            }
        }
        finally
        {
            Interlocked.Exchange(ref _isProcessing, 0);

            lock (_stateLock)
            {
                if (_refreshRequested && !_isDisposed)
                {
                    _ = ProcessPendingRefreshesAsync();
                }
            }
        }
    }

    private bool TryConsumeRefreshRequest()
    {
        lock (_stateLock)
        {
            if (_isDisposed || !_refreshRequested)
            {
                return false;
            }

            _refreshRequested = false;
            return true;
        }
    }

    private async Task RefreshConnectionAsync(CancellationToken cancellationToken)
    {
        var resourceNumber = string.Empty;

        try
        {
            var configurationResolution = await _plcClientConfigurationResolver.ResolveAsync(cancellationToken).ConfigureAwait(false);
            resourceNumber = configurationResolution.ResourceNumber;
            if (!configurationResolution.IsConfigured)
            {
                await _plcOperationPipeline.DisconnectAsync(PlcConfigurationPipelineOperations.DisconnectWhenNotConfigured, cancellationToken).ConfigureAwait(false);
                _lastAppliedConfigurationKey = null;
                _plcConnectionStatusService.Set(PlcStartupConnectionResult.NotConfigured());
                return;
            }

            var clientAppInfo = configurationResolution.ClientAppInfo!;

            var configurationKey = CreateConfigurationKey(clientAppInfo);
            if (string.Equals(_lastAppliedConfigurationKey, configurationKey, StringComparison.Ordinal))
            {
                return;
            }

            try
            {
                _plcConnectionStatusService.Set(PlcStartupConnectionResult.Connecting(LocalizedText.Get("Services.PlcStartupConnection.Reconnecting")));
                await _plcOperationPipeline.ConnectAsync(PlcConfigurationPipelineOperations.ApplyAndReconnect, PlcConnectionOptionsFactory.Create(clientAppInfo), cancellationToken).ConfigureAwait(false);

                _lastAppliedConfigurationKey = configurationKey;
                _plcConnectionStatusService.Set(PlcStartupConnectionResult.Connected(LocalizedText.Get("Services.PlcStartupConnection.Reconfigured")));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, LocalizedText.Get("Services.PlcConfigurationMonitor.LogApplySettingsFailed"), clientAppInfo.ResourceNumber);

                var isConnected = await _plcOperationPipeline.IsConnectedAsync(PlcConfigurationPipelineOperations.CheckConnectionState, cancellationToken).ConfigureAwait(false);
                if (isConnected)
                {
                    _plcConnectionStatusService.Set(PlcStartupConnectionResult.Connected(
                        LocalizedText.Format("Services.PlcStartupConnection.ReconfiguredFailedKeepCurrent", ex.Message)));
                    return;
                }

                _plcConnectionStatusService.Set(PlcStartupConnectionResult.Failed(
                    LocalizedText.Format("Services.PlcStartupConnection.ConnectFailed", ex.Message)));
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, LocalizedText.Get("Services.PlcConfigurationMonitor.LogApplySettingsFailed"), resourceNumber);
            _plcConnectionStatusService.Set(PlcStartupConnectionResult.Failed(
                LocalizedText.Format("Services.PlcStartupConnection.ConnectFailed", ex.Message)));
        }
    }

    private static string CreateConfigurationKey(ClientAppInfoModel model)
    {
        return string.Join('|',
            model.ResourceNumber?.Trim() ?? string.Empty,
            model.PlcProtocolType?.Trim() ?? string.Empty,
            model.PlcIpAddress?.Trim() ?? string.Empty,
            model.PlcPort,
            model.SiemensRack,
            model.SiemensSlot,
            model.IsStringReverse);
    }
}