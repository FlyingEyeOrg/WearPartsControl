using System.Threading;
using Microsoft.Extensions.DependencyInjection;
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
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly IPlcOperationPipeline _plcOperationPipeline;
    private readonly IPlcConnectionStatusService _plcConnectionStatusService;
    private readonly ILogger<PlcConfigurationMonitorService> _logger;
    private string? _lastAppliedConfigurationKey;
    private bool _refreshRequested;
    private int _isProcessing;
    private bool _isDisposed;

    public PlcConfigurationMonitorService(
        IAppSettingsService appSettingsService,
        IServiceScopeFactory serviceScopeFactory,
        IPlcOperationPipeline plcOperationPipeline,
        IPlcConnectionStatusService plcConnectionStatusService,
        ILogger<PlcConfigurationMonitorService> logger)
    {
        _appSettingsService = appSettingsService;
        _serviceScopeFactory = serviceScopeFactory;
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
        var settings = await _appSettingsService.GetAsync(cancellationToken).ConfigureAwait(false);
        if (!settings.IsSetClientAppInfo || string.IsNullOrWhiteSpace(settings.ResourceNumber))
        {
            await _plcOperationPipeline.ExecuteAsync("Config/DisconnectWhenNotConfigured", plcService => plcService.Disconnect(), cancellationToken).ConfigureAwait(false);
            _lastAppliedConfigurationKey = null;
            _plcConnectionStatusService.Set(PlcStartupConnectionResult.NotConfigured());
            return;
        }

        await using var scope = _serviceScopeFactory.CreateAsyncScope();
        var clientAppInfoService = scope.ServiceProvider.GetRequiredService<IClientAppInfoService>();
        var clientAppInfo = await clientAppInfoService.GetAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(clientAppInfo.ResourceNumber)
            || string.IsNullOrWhiteSpace(clientAppInfo.PlcProtocolType)
            || string.IsNullOrWhiteSpace(clientAppInfo.PlcIpAddress))
        {
            await _plcOperationPipeline.ExecuteAsync("Config/DisconnectWhenInvalid", plcService => plcService.Disconnect(), cancellationToken).ConfigureAwait(false);
            _lastAppliedConfigurationKey = null;
            _plcConnectionStatusService.Set(PlcStartupConnectionResult.NotConfigured());
            return;
        }

        var configurationKey = CreateConfigurationKey(clientAppInfo);
        if (string.Equals(_lastAppliedConfigurationKey, configurationKey, StringComparison.Ordinal))
        {
            return;
        }

        try
        {
            _plcConnectionStatusService.Set(PlcStartupConnectionResult.Connecting(LocalizedText.Get("Services.PlcStartupConnection.Reconnecting")));
            await _plcOperationPipeline.ExecuteAsync("Config/ApplyAndReconnect", async plcService =>
            {
                await plcService.ConnectAsync(PlcConnectionOptionsFactory.Create(clientAppInfo), cancellationToken).ConfigureAwait(false);
            }, cancellationToken).ConfigureAwait(false);

            _lastAppliedConfigurationKey = configurationKey;
            _plcConnectionStatusService.Set(PlcStartupConnectionResult.Connected(LocalizedText.Get("Services.PlcStartupConnection.Reconfigured")));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Failed to apply PLC settings change for resource {ResourceNumber}", clientAppInfo.ResourceNumber);

            var isConnected = await _plcOperationPipeline.ExecuteAsync("Config/CheckConnectionState", plcService => plcService.IsConnected, cancellationToken).ConfigureAwait(false);
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