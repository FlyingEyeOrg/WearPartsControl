using System.Net;
using HslCommunication;
using HslCommunication.Core.Device;
using HslCommunication.ModBus;
using HslCommunication.Profinet;
using HslCommunication.Profinet.AllenBradley;
using HslCommunication.Profinet.Beckhoff;
using HslCommunication.Profinet.Inovance;
using HslCommunication.Profinet.Keyence;
using HslCommunication.Profinet.Omron;
using PlcGateway.Core;
using PlcGateway.Drivers.Beckhoff;
using PlcGateway.Drivers.Inovance;
using TwinCAT.Ads;
using WearPartsControl.ApplicationServices.Localization;
using WearPartsControl.Exceptions;

namespace WearPartsControl.ApplicationServices.PlcService;

public sealed class PlcService : IPlcService, IPlcOperationContext, IDisposable
{
    private readonly object _syncRoot = new();
    private readonly ILocalizationService _localizationService;
    private PlcClientSession? _clientSession;
    private PlcConnectionOptions? _currentOptions;
    private bool _isDisposed;

    public PlcService(ILocalizationService localizationService)
    {
        _localizationService = localizationService;
    }

    public bool IsConnected { get; private set; }

    public async Task ConnectAsync(PlcConnectionOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        var resolvedOptions = await NormalizeConnectionOptionsAsync(options, cancellationToken).ConfigureAwait(false);
        PlcClientSession? replacementSession = null;
        PlcClientSession? previousSession = null;

        lock (_syncRoot)
        {
            ThrowIfDisposed();

            if (IsConnected && _currentOptions == resolvedOptions)
            {
                return;
            }

            replacementSession = CreateClientSession(resolvedOptions);

            try
            {
                ConnectInternal(replacementSession, resolvedOptions.ConnectTimeoutMilliseconds);
                previousSession = _clientSession;
                _clientSession = replacementSession;
                _currentOptions = resolvedOptions;
                IsConnected = true;
            }
            catch
            {
                replacementSession.Dispose();
                throw;
            }
        }

        previousSession?.Dispose();
    }

    public void Disconnect()
    {
        PlcClientSession? previousSession;

        lock (_syncRoot)
        {
            ThrowIfDisposed();
            previousSession = DisconnectInternal();
            _currentOptions = null;
        }

        previousSession?.Dispose();
    }

    public TValue Read<TValue>(string address, int retryCount = 1)
    {
        lock (_syncRoot)
        {
            ThrowIfDisposed();
            if (string.IsNullOrWhiteSpace(address))
            {
                throw new ArgumentException(L("PlcService.Errors.ReadAddressEmpty"), nameof(address));
            }

            EnsureConnected();
            return ExecuteWithReconnectRetry(retryCount, () => ReadCore<TValue>(address));
        }
    }

    public void Write<TValue>(string address, TValue value, int retryCount = 1)
    {
        lock (_syncRoot)
        {
            ThrowIfDisposed();
            if (string.IsNullOrWhiteSpace(address))
            {
                throw new ArgumentException(L("PlcService.Errors.WriteAddressEmpty"), nameof(address));
            }

            if (value is null)
            {
                throw new ArgumentNullException(nameof(value), L("PlcService.Errors.WriteValueNull"));
            }

            EnsureConnected();
            ExecuteWithReconnectRetry(retryCount, () =>
            {
                WriteCore(address, value);
                return true;
            });
        }
    }

    public void Dispose()
    {
        PlcClientSession? previousSession;

        lock (_syncRoot)
        {
            if (_isDisposed)
            {
                return;
            }

            previousSession = DisconnectInternal();
            _isDisposed = true;
        }

        previousSession?.Dispose();
    }

    private PlcClientSession CreateClientSession(PlcConnectionOptions options)
    {
        switch (options.PlcType)
        {
            case PlcProtocolType.OmronCip:
                return new PlcClientSession { DeviceTcpNet = new OmronCipNet(options.IpAddress, options.Port) };
            case PlcProtocolType.OmronFins:
                return new PlcClientSession { DeviceTcpNet = new OmronFinsNet(options.IpAddress, options.Port) };
            case PlcProtocolType.Mitsubishi:
                return new PlcClientSession { DeviceTcpNet = new HslCommunication.Profinet.Melsec.MelsecCipNet(options.IpAddress, options.Port) };
            case PlcProtocolType.SiemensS1500:
                return new PlcClientSession { DeviceTcpNet = BuildSiemensClient(HslCommunication.Profinet.Siemens.SiemensPLCS.S1500, options) };
            case PlcProtocolType.SiemensS1200:
                return new PlcClientSession { DeviceTcpNet = BuildSiemensClient(HslCommunication.Profinet.Siemens.SiemensPLCS.S1200, options) };
            case PlcProtocolType.AllenBradley:
                return new PlcClientSession { DeviceTcpNet = new AllenBradleyNet(options.IpAddress, options.Port) };
            case PlcProtocolType.InovanceAm:
                return new PlcClientSession
                {
                    DeviceTcpNet = new InovanceTcpNet(InovanceSeries.AM, options.IpAddress, options.Port)
                    {
                        IsStringReverse = options.IsStringReverse
                    }
                };
            case PlcProtocolType.InovanceH3U:
                return new PlcClientSession { DeviceTcpNet = new InovanceTcpNet(InovanceSeries.H3U, options.IpAddress, options.Port) };
            case PlcProtocolType.InovanceH5U:
                return new PlcClientSession { DeviceTcpNet = new InovanceTcpNet(InovanceSeries.H5U, options.IpAddress, options.Port) };
            case PlcProtocolType.InovanceEip:
                return new PlcClientSession { InovanceEipDriver = BuildInovanceEipClient(options) };
            case PlcProtocolType.Beckhoff:
                return new PlcClientSession { BeckhoffAdsSymbolDriver = BuildBeckhoffSymbolDriver(options) };
            case PlcProtocolType.Keyence:
                return new PlcClientSession { DeviceTcpNet = new KeyenceMcNet(options.IpAddress, options.Port) };
            case PlcProtocolType.ModbusTcp:
                return new PlcClientSession
                {
                    DeviceTcpNet = new ModbusTcpNet(options.IpAddress, options.Port)
                    {
                        IsStringReverse = options.IsStringReverse
                    }
                };
            default:
                throw new NotSupportedException($"{L("PlcService.Errors.ProtocolNotSupported")} : {options.PlcType}");
        }
    }

    private BeckhoffAdsSymbolDriver BuildBeckhoffSymbolDriver(PlcConnectionOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.BeckhoffAmsNetId))
        {
            throw new BusinessException(L("PlcService.Errors.BeckhoffAmsNetIdMissing"));
        }

        var amsNetId = new AmsNetId(options.BeckhoffAmsNetId);
        var amsPort = (AmsPort)options.BeckhoffAmsPort;
        return new BeckhoffAdsSymbolDriver(amsNetId, amsPort);
    }

    private static HslCommunication.Profinet.Siemens.SiemensS7Net BuildSiemensClient(
        HslCommunication.Profinet.Siemens.SiemensPLCS plcType,
        PlcConnectionOptions options)
    {
        var client = new HslCommunication.Profinet.Siemens.SiemensS7Net(plcType, options.IpAddress)
        {
            Rack = (byte)options.SiemensRack,
            Slot = (byte)options.SiemensSlot
        };

        if (options.Port > 0 && options.Port < 65535)
        {
            client.Port = options.Port;
        }

        return client;
    }

    private InovanceEIPDriver BuildInovanceEipClient(PlcConnectionOptions options)
    {
        if (!IPAddress.TryParse(options.IpAddress, out var targetIpAddress))
        {
            throw new BusinessException(L("PlcService.Errors.TargetIpInvalid"));
        }

        var hostIpAddress = ResolveHostIpAddress(options.HostIpAddress, targetIpAddress);
        return new InovanceEIPDriver(hostIpAddress, targetIpAddress);
    }

    private async Task<PlcConnectionOptions> NormalizeConnectionOptionsAsync(PlcConnectionOptions options, CancellationToken cancellationToken)
    {
        if (options.PlcType != PlcProtocolType.InovanceEip || !string.IsNullOrWhiteSpace(options.HostIpAddress))
        {
            return options;
        }

        if (!IPAddress.TryParse(options.IpAddress, out var targetIpAddress))
        {
            throw new BusinessException(L("PlcService.Errors.TargetIpInvalid"));
        }

        var scannedAddress = await NetworkAddressScanner.TryScanAsync(targetIpAddress).WaitAsync(cancellationToken).ConfigureAwait(false);
        if (scannedAddress is null)
        {
            throw new BusinessException(L("PlcService.Errors.HostIpNotFoundInSubnet"));
        }

        return options with { HostIpAddress = scannedAddress.ToString() };
    }

    private IPAddress ResolveHostIpAddress(string? configuredHostIp, IPAddress targetIpAddress)
    {
        if (!string.IsNullOrWhiteSpace(configuredHostIp))
        {
            if (IPAddress.TryParse(configuredHostIp, out var configuredAddress))
            {
                return configuredAddress;
            }

            throw new BusinessException(L("PlcService.Errors.HostIpInvalid"));
        }

        throw new BusinessException(L("PlcService.Errors.HostIpNotFoundInSubnet"));
    }

    private void ConnectInternal(PlcClientSession session, int timeoutMilliseconds)
    {
        if (session.InovanceEipDriver is not null)
        {
            session.InovanceEipDriver.Connect();
            return;
        }

        if (session.BeckhoffAdsSymbolDriver is not null)
        {
            session.BeckhoffAdsSymbolDriver.Connect();
            return;
        }

        if (session.DeviceTcpNet is null)
        {
            throw new InvalidOperationException(L("PlcService.Errors.ClientNotInitialized"));
        }

        session.DeviceTcpNet.ConnectTimeOut = timeoutMilliseconds;
        var result = session.DeviceTcpNet.ConnectServer();
        if (!result.IsSuccess)
        {
            throw new BusinessException($"{L("PlcService.Errors.ConnectFailed")}: {result.Message}", details: $"ErrorCode={result.ErrorCode}");
        }
    }

    private void EnsureConnected()
    {
        if (IsConnected)
        {
            return;
        }

        ReconnectInternal();
    }

    private void ReconnectInternal()
    {
        if (_currentOptions is null)
        {
            throw new InvalidOperationException(L("PlcService.Errors.ReconnectWithoutOptions"));
        }

        var replacementSession = CreateClientSession(_currentOptions);

        try
        {
            ConnectInternal(replacementSession, _currentOptions.ConnectTimeoutMilliseconds);
        }
        catch
        {
            replacementSession.Dispose();
            throw;
        }

        var previousSession = _clientSession;
        _clientSession = replacementSession;
        IsConnected = true;
        previousSession?.Dispose();
    }

    private PlcClientSession? DisconnectInternal()
    {
        var previousSession = _clientSession;
        _clientSession = null;
        IsConnected = false;
        return previousSession;
    }

    private TValue ReadCore<TValue>(string address)
    {
        var clientSession = _clientSession;

        if (clientSession?.InovanceEipDriver is not null)
        {
            return clientSession.InovanceEipDriver.Read<TValue>(address);
        }

        if (clientSession?.BeckhoffAdsSymbolDriver is not null)
        {
            return clientSession.BeckhoffAdsSymbolDriver.Read<TValue>(address);
        }

        if (clientSession?.DeviceTcpNet is null)
        {
            throw new InvalidOperationException(L("PlcService.Errors.ClientNotInitialized"));
        }

        if (typeof(TValue) == typeof(string))
        {
            var stringLength = (ushort)Math.Clamp(_currentOptions?.StringReadLength ?? 99, 1, ushort.MaxValue);
            var result = clientSession.DeviceTcpNet.ReadString(address, stringLength);
            EnsureOperateSuccess(result, L("PlcService.Errors.ReadFailed"));
            return (TValue)(object)(result.Content ?? string.Empty);
        }

        if (typeof(TValue) == typeof(bool))
        {
            var result = clientSession.DeviceTcpNet.ReadBool(address);
            EnsureOperateSuccess(result, L("PlcService.Errors.ReadFailed"));
            return (TValue)(object)result.Content;
        }

        if (typeof(TValue) == typeof(short))
        {
            var result = clientSession.DeviceTcpNet.ReadInt16(address);
            EnsureOperateSuccess(result, L("PlcService.Errors.ReadFailed"));
            return (TValue)(object)result.Content;
        }

        if (typeof(TValue) == typeof(int))
        {
            var result = clientSession.DeviceTcpNet.ReadInt32(address);
            EnsureOperateSuccess(result, L("PlcService.Errors.ReadFailed"));
            return (TValue)(object)result.Content;
        }

        if (typeof(TValue) == typeof(uint))
        {
            var result = clientSession.DeviceTcpNet.ReadUInt32(address);
            EnsureOperateSuccess(result, L("PlcService.Errors.ReadFailed"));
            return (TValue)(object)result.Content;
        }

        if (typeof(TValue) == typeof(float))
        {
            var result = clientSession.DeviceTcpNet.ReadFloat(address);
            EnsureOperateSuccess(result, L("PlcService.Errors.ReadFailed"));
            return (TValue)(object)result.Content;
        }

        if (typeof(TValue) == typeof(double))
        {
            var result = clientSession.DeviceTcpNet.ReadDouble(address);
            EnsureOperateSuccess(result, L("PlcService.Errors.ReadFailed"));
            return (TValue)(object)result.Content;
        }

        throw new NotSupportedException($"{L("PlcService.Errors.ReadDataTypeNotSupported")}: {typeof(TValue).Name}");
    }

    private void WriteCore<TValue>(string address, TValue value)
    {
        var clientSession = _clientSession;

        if (clientSession?.InovanceEipDriver is not null)
        {
            clientSession.InovanceEipDriver.Write(address, value);
            return;
        }

        if (clientSession?.BeckhoffAdsSymbolDriver is not null)
        {
            clientSession.BeckhoffAdsSymbolDriver.Write(address, value);
            return;
        }

        if (clientSession?.DeviceTcpNet is null)
        {
            throw new InvalidOperationException(L("PlcService.Errors.ClientNotInitialized"));
        }

        OperateResult writeResult;

        if (value is bool boolValue)
        {
            writeResult = clientSession.DeviceTcpNet.Write(address, boolValue);
            EnsureOperateSuccess(writeResult, L("PlcService.Errors.WriteFailed"));
            return;
        }

        if (value is short shortValue)
        {
            writeResult = clientSession.DeviceTcpNet.Write(address, shortValue);
            EnsureOperateSuccess(writeResult, L("PlcService.Errors.WriteFailed"));
            return;
        }

        if (value is int intValue)
        {
            writeResult = clientSession.DeviceTcpNet.Write(address, intValue);
            EnsureOperateSuccess(writeResult, L("PlcService.Errors.WriteFailed"));
            return;
        }

        if (value is uint uintValue)
        {
            writeResult = clientSession.DeviceTcpNet.Write(address, uintValue);
            EnsureOperateSuccess(writeResult, L("PlcService.Errors.WriteFailed"));
            return;
        }

        if (value is float floatValue)
        {
            writeResult = clientSession.DeviceTcpNet.Write(address, floatValue);
            EnsureOperateSuccess(writeResult, L("PlcService.Errors.WriteFailed"));
            return;
        }

        if (value is double doubleValue)
        {
            writeResult = clientSession.DeviceTcpNet.Write(address, doubleValue);
            EnsureOperateSuccess(writeResult, L("PlcService.Errors.WriteFailed"));
            return;
        }

        if (value is string stringValue)
        {
            writeResult = clientSession.DeviceTcpNet.Write(address, stringValue);
            EnsureOperateSuccess(writeResult, L("PlcService.Errors.WriteFailed"));
            return;
        }

        throw new NotSupportedException($"{L("PlcService.Errors.WriteDataTypeNotSupported")}: {typeof(TValue).Name}");
    }

    private static void EnsureOperateSuccess(OperateResult result, string action)
    {
        if (result.IsSuccess)
        {
            return;
        }

        throw new BusinessException($"{action}: {result.Message}", details: $"ErrorCode={result.ErrorCode}");
    }

    private T ExecuteWithReconnectRetry<T>(int retryCount, Func<T> action)
    {
        Exception? lastException = null;
        var totalAttempts = Math.Max(1, retryCount + 1);

        for (var attempt = 1; attempt <= totalAttempts; attempt++)
        {
            try
            {
                return action();
            }
            catch (Exception ex)
            {
                lastException = ex;
                if (attempt < totalAttempts)
                {
                    ReconnectInternal();
                }
            }
        }

        throw new BusinessException(L("PlcService.Errors.OperationRetryFailed"), details: L("PlcService.Errors.RetryStillFailed"), innerException: lastException);
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
    }

    private string L(string key) => _localizationService[key];

    private sealed class PlcClientSession : IDisposable
    {
        public DeviceTcpNet? DeviceTcpNet { get; init; }

        public InovanceEIPDriver? InovanceEipDriver { get; init; }

        public BeckhoffAdsSymbolDriver? BeckhoffAdsSymbolDriver { get; init; }

        public void Dispose()
        {
            try
            {
                InovanceEipDriver?.Disconnect();
                BeckhoffAdsSymbolDriver?.Disconnect();
                BeckhoffAdsSymbolDriver?.Dispose();
                _ = DeviceTcpNet?.ConnectClose();
            }
            catch
            {
            }
        }
    }
}
