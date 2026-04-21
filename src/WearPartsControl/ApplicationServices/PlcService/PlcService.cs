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

public sealed class PlcService : IPlcService, IDisposable
{
    private readonly object _syncRoot = new();
    private readonly ILocalizationService _localizationService;
    private DeviceTcpNet? _deviceTcpNet;
    private InovanceEIPDriver? _inovanceEipDriver;
    private BeckhoffAdsSymbolDriver? _beckhoffAdsSymbolDriver;
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

        lock (_syncRoot)
        {
            ThrowIfDisposed();

            if (IsConnected && _currentOptions == resolvedOptions)
            {
                return;
            }

            DisconnectInternal();

            try
            {
                InitializeClient(resolvedOptions);
                ConnectInternal(resolvedOptions.ConnectTimeoutMilliseconds);
                _currentOptions = resolvedOptions;
            }
            catch
            {
                DisconnectInternal();
                throw;
            }
        }
    }

    public void Disconnect()
    {
        lock (_syncRoot)
        {
            ThrowIfDisposed();
            DisconnectInternal();
            _currentOptions = null;
        }
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
        lock (_syncRoot)
        {
            if (_isDisposed)
            {
                return;
            }

            DisconnectInternal();
            _isDisposed = true;
        }
    }

    private void InitializeClient(PlcConnectionOptions options)
    {
        switch (options.PlcType)
        {
            case PlcProtocolType.OmronCip:
                _deviceTcpNet = new OmronCipNet(options.IpAddress, options.Port);
                break;
            case PlcProtocolType.OmronFins:
                _deviceTcpNet = new OmronFinsNet(options.IpAddress, options.Port);
                break;
            case PlcProtocolType.Mitsubishi:
                _deviceTcpNet = new HslCommunication.Profinet.Melsec.MelsecCipNet(options.IpAddress, options.Port);
                break;
            case PlcProtocolType.SiemensS1500:
                _deviceTcpNet = BuildSiemensClient(HslCommunication.Profinet.Siemens.SiemensPLCS.S1500, options);
                break;
            case PlcProtocolType.SiemensS1200:
                _deviceTcpNet = BuildSiemensClient(HslCommunication.Profinet.Siemens.SiemensPLCS.S1200, options);
                break;
            case PlcProtocolType.AllenBradley:
                _deviceTcpNet = new AllenBradleyNet(options.IpAddress, options.Port);
                break;
            case PlcProtocolType.InovanceAm:
                _deviceTcpNet = new InovanceTcpNet(InovanceSeries.AM, options.IpAddress, options.Port)
                {
                    IsStringReverse = options.IsStringReverse
                };
                break;
            case PlcProtocolType.InovanceH3U:
                _deviceTcpNet = new InovanceTcpNet(InovanceSeries.H3U, options.IpAddress, options.Port);
                break;
            case PlcProtocolType.InovanceH5U:
                _deviceTcpNet = new InovanceTcpNet(InovanceSeries.H5U, options.IpAddress, options.Port);
                break;
            case PlcProtocolType.InovanceEip:
                _inovanceEipDriver = BuildInovanceEipClient(options);
                break;
            case PlcProtocolType.Beckhoff:
                _beckhoffAdsSymbolDriver = BuildBeckhoffSymbolDriver(options);
                break;
            case PlcProtocolType.Keyence:
                _deviceTcpNet = new KeyenceMcNet(options.IpAddress, options.Port);
                break;
            case PlcProtocolType.ModbusTcp:
                _deviceTcpNet = new ModbusTcpNet(options.IpAddress, options.Port)
                {
                    IsStringReverse = options.IsStringReverse
                };
                break;
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

    private void ConnectInternal(int timeoutMilliseconds)
    {
        if (_inovanceEipDriver is not null)
        {
            _inovanceEipDriver.Connect();
            IsConnected = true;
            return;
        }

        if (_beckhoffAdsSymbolDriver is not null)
        {
            _beckhoffAdsSymbolDriver.Connect();
            IsConnected = true;
            return;
        }

        if (_deviceTcpNet is null)
        {
            IsConnected = false;
            throw new InvalidOperationException(L("PlcService.Errors.ClientNotInitialized"));
        }

        _deviceTcpNet.ConnectTimeOut = timeoutMilliseconds;
        var result = _deviceTcpNet.ConnectServer();
        if (!result.IsSuccess)
        {
            IsConnected = false;
            throw new BusinessException($"{L("PlcService.Errors.ConnectFailed")}: {result.Message}", details: $"ErrorCode={result.ErrorCode}");
        }

        IsConnected = true;
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

        DisconnectInternal();
        InitializeClient(_currentOptions);
        ConnectInternal(_currentOptions.ConnectTimeoutMilliseconds);
    }

    private void DisconnectInternal()
    {
        try
        {
            if (_inovanceEipDriver is not null)
            {
                _inovanceEipDriver.Disconnect();
            }

            if (_beckhoffAdsSymbolDriver is not null)
            {
                _beckhoffAdsSymbolDriver.Disconnect();
                _beckhoffAdsSymbolDriver.Dispose();
            }

            if (_deviceTcpNet is not null)
            {
                _ = _deviceTcpNet.ConnectClose();
            }
        }
        finally
        {
            _inovanceEipDriver = null;
            _beckhoffAdsSymbolDriver = null;
            _deviceTcpNet = null;
            IsConnected = false;
        }
    }

    private TValue ReadCore<TValue>(string address)
    {
        if (_inovanceEipDriver is not null)
        {
            return _inovanceEipDriver.Read<TValue>(address);
        }

        if (_beckhoffAdsSymbolDriver is not null)
        {
            return _beckhoffAdsSymbolDriver.Read<TValue>(address);
        }

        if (_deviceTcpNet is null)
        {
            throw new InvalidOperationException(L("PlcService.Errors.ClientNotInitialized"));
        }

        if (typeof(TValue) == typeof(string))
        {
            var stringLength = (ushort)Math.Clamp(_currentOptions?.StringReadLength ?? 99, 1, ushort.MaxValue);
            var result = _deviceTcpNet.ReadString(address, stringLength);
            EnsureOperateSuccess(result, L("PlcService.Errors.ReadFailed"));
            return (TValue)(object)(result.Content ?? string.Empty);
        }

        if (typeof(TValue) == typeof(bool))
        {
            var result = _deviceTcpNet.ReadBool(address);
            EnsureOperateSuccess(result, L("PlcService.Errors.ReadFailed"));
            return (TValue)(object)result.Content;
        }

        if (typeof(TValue) == typeof(short))
        {
            var result = _deviceTcpNet.ReadInt16(address);
            EnsureOperateSuccess(result, L("PlcService.Errors.ReadFailed"));
            return (TValue)(object)result.Content;
        }

        if (typeof(TValue) == typeof(int))
        {
            var result = _deviceTcpNet.ReadInt32(address);
            EnsureOperateSuccess(result, L("PlcService.Errors.ReadFailed"));
            return (TValue)(object)result.Content;
        }

        if (typeof(TValue) == typeof(uint))
        {
            var result = _deviceTcpNet.ReadUInt32(address);
            EnsureOperateSuccess(result, L("PlcService.Errors.ReadFailed"));
            return (TValue)(object)result.Content;
        }

        if (typeof(TValue) == typeof(float))
        {
            var result = _deviceTcpNet.ReadFloat(address);
            EnsureOperateSuccess(result, L("PlcService.Errors.ReadFailed"));
            return (TValue)(object)result.Content;
        }

        if (typeof(TValue) == typeof(double))
        {
            var result = _deviceTcpNet.ReadDouble(address);
            EnsureOperateSuccess(result, L("PlcService.Errors.ReadFailed"));
            return (TValue)(object)result.Content;
        }

        throw new NotSupportedException($"{L("PlcService.Errors.ReadDataTypeNotSupported")}: {typeof(TValue).Name}");
    }

    private void WriteCore<TValue>(string address, TValue value)
    {
        if (_inovanceEipDriver is not null)
        {
            _inovanceEipDriver.Write(address, value);
            return;
        }

        if (_beckhoffAdsSymbolDriver is not null)
        {
            _beckhoffAdsSymbolDriver.Write(address, value);
            return;
        }

        if (_deviceTcpNet is null)
        {
            throw new InvalidOperationException(L("PlcService.Errors.ClientNotInitialized"));
        }

        OperateResult writeResult;

        if (value is bool boolValue)
        {
            writeResult = _deviceTcpNet.Write(address, boolValue);
            EnsureOperateSuccess(writeResult, L("PlcService.Errors.WriteFailed"));
            return;
        }

        if (value is short shortValue)
        {
            writeResult = _deviceTcpNet.Write(address, shortValue);
            EnsureOperateSuccess(writeResult, L("PlcService.Errors.WriteFailed"));
            return;
        }

        if (value is int intValue)
        {
            writeResult = _deviceTcpNet.Write(address, intValue);
            EnsureOperateSuccess(writeResult, L("PlcService.Errors.WriteFailed"));
            return;
        }

        if (value is uint uintValue)
        {
            writeResult = _deviceTcpNet.Write(address, uintValue);
            EnsureOperateSuccess(writeResult, L("PlcService.Errors.WriteFailed"));
            return;
        }

        if (value is float floatValue)
        {
            writeResult = _deviceTcpNet.Write(address, floatValue);
            EnsureOperateSuccess(writeResult, L("PlcService.Errors.WriteFailed"));
            return;
        }

        if (value is double doubleValue)
        {
            writeResult = _deviceTcpNet.Write(address, doubleValue);
            EnsureOperateSuccess(writeResult, L("PlcService.Errors.WriteFailed"));
            return;
        }

        if (value is string stringValue)
        {
            writeResult = _deviceTcpNet.Write(address, stringValue);
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
}
