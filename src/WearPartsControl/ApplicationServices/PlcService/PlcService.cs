using System.Globalization;
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
using WearPartsControl.Exceptions;

namespace WearPartsControl.ApplicationServices.PlcService;

public sealed class PlcService : IPlcService, IDisposable
{
    private readonly object _syncRoot = new();
    private DeviceTcpNet? _deviceTcpNet;
    private InovanceEIPDriver? _inovanceEipDriver;
    private BeckhoffAdsSymbolDriver? _beckhoffAdsSymbolDriver;
    private PlcConnectionOptions? _currentOptions;
    private bool _isDisposed;

    public bool IsConnected { get; private set; }

    public void Connect(PlcConnectionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        lock (_syncRoot)
        {
            ThrowIfDisposed();

            if (IsConnected && _currentOptions == options)
            {
                return;
            }

            DisconnectInternal();

            try
            {
                InitializeClient(options);
                ConnectInternal(options.ConnectTimeoutMilliseconds);
                _currentOptions = options;
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

    public string ReadAsString(string address, PlcDataType dataType, int retryCount = 1)
    {
        lock (_syncRoot)
        {
            ThrowIfDisposed();
            if (string.IsNullOrWhiteSpace(address))
            {
                throw new ArgumentException("读取地址为空", nameof(address));
            }

            EnsureConnected();
            return ExecuteWithReconnectRetry(retryCount, () => ReadCore(address, dataType));
        }
    }

    public void WriteFromString(string address, PlcDataType dataType, string value, int retryCount = 1)
    {
        lock (_syncRoot)
        {
            ThrowIfDisposed();
            if (string.IsNullOrWhiteSpace(address))
            {
                throw new ArgumentException("写入地址为空", nameof(address));
            }

            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("写入数据为空", nameof(value));
            }

            EnsureConnected();
            ExecuteWithReconnectRetry(retryCount, () =>
            {
                WriteCore(address, dataType, value);
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
                throw new NotSupportedException($"不支持的 PLC 类型: {options.PlcType}");
        }
    }

    private static BeckhoffAdsSymbolDriver BuildBeckhoffSymbolDriver(PlcConnectionOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.BeckhoffAmsNetId))
        {
            throw new BusinessException("倍福 PLC 连接缺少 BeckhoffAmsNetId");
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

    private static InovanceEIPDriver BuildInovanceEipClient(PlcConnectionOptions options)
    {
        if (!IPAddress.TryParse(options.IpAddress, out var targetIpAddress))
        {
            throw new BusinessException("PLC 目标 IP 地址格式错误");
        }

        var hostIpAddress = ResolveHostIpAddress(options.HostIpAddress, targetIpAddress);
        return new InovanceEIPDriver(hostIpAddress, targetIpAddress);
    }

    private static IPAddress ResolveHostIpAddress(string? configuredHostIp, IPAddress targetIpAddress)
    {
        if (!string.IsNullOrWhiteSpace(configuredHostIp))
        {
            if (IPAddress.TryParse(configuredHostIp, out var configuredAddress))
            {
                return configuredAddress;
            }

            throw new BusinessException("主机 IP 地址格式错误");
        }

        var scannedAddress = NetworkAddressScanner.TryScanAsync(targetIpAddress).GetAwaiter().GetResult();
        if (scannedAddress is null)
        {
            throw new BusinessException("未找到与 PLC 同网段的本地网卡地址，请配置 HostIpAddress");
        }

        return scannedAddress;
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
            throw new InvalidOperationException("PLC 客户端未初始化");
        }

        _deviceTcpNet.ConnectTimeOut = timeoutMilliseconds;
        var result = _deviceTcpNet.ConnectServer();
        if (!result.IsSuccess)
        {
            IsConnected = false;
            throw new BusinessException($"连接失败: {result.Message}", details: $"ErrorCode={result.ErrorCode}");
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
            throw new InvalidOperationException("PLC 尚未配置连接参数，无法重连");
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

    private string ReadCore(string address, PlcDataType dataType)
    {
        if (_inovanceEipDriver is not null)
        {
            return _inovanceEipDriver.ReadByType(address, dataType);
        }

        if (_beckhoffAdsSymbolDriver is not null)
        {
            return dataType switch
            {
                PlcDataType.Int => PlcTypeConversion.ToInvariantString(_beckhoffAdsSymbolDriver.Read<short>(address)),
                PlcDataType.DInt => PlcTypeConversion.ToInvariantString(_beckhoffAdsSymbolDriver.Read<int>(address)),
                PlcDataType.UDInt => PlcTypeConversion.ToInvariantString(_beckhoffAdsSymbolDriver.Read<uint>(address)),
                PlcDataType.Bool => PlcTypeConversion.ToInvariantString(_beckhoffAdsSymbolDriver.Read<bool>(address)),
                PlcDataType.Real => PlcTypeConversion.ToInvariantString(_beckhoffAdsSymbolDriver.Read<float>(address)),
                PlcDataType.LReal => PlcTypeConversion.ToInvariantString(_beckhoffAdsSymbolDriver.Read<double>(address)),
                PlcDataType.String => _beckhoffAdsSymbolDriver.Read<string>(address),
                _ => throw new NotSupportedException($"不支持读取的数据类型: {dataType}")
            };
        }

        if (_deviceTcpNet is null)
        {
            throw new InvalidOperationException("PLC 客户端未初始化");
        }

        var stringLength = (ushort)Math.Clamp(_currentOptions?.StringReadLength ?? 99, 1, ushort.MaxValue);
        return dataType switch
        {
            PlcDataType.Int => ReadAndConvert(_deviceTcpNet.ReadInt16(address)),
            PlcDataType.DInt => ReadAndConvert(_deviceTcpNet.ReadInt32(address)),
            PlcDataType.UDInt => ReadAndConvert(_deviceTcpNet.ReadUInt32(address)),
            PlcDataType.Bool => ReadAndConvert(_deviceTcpNet.ReadBool(address)),
            PlcDataType.Real => ReadAndConvert(_deviceTcpNet.ReadFloat(address)),
            PlcDataType.LReal => ReadAndConvert(_deviceTcpNet.ReadDouble(address)),
            PlcDataType.String => ReadAndConvert(_deviceTcpNet.ReadString(address, stringLength)),
            _ => throw new NotSupportedException($"不支持读取的数据类型: {dataType}")
        };
    }

    private void WriteCore(string address, PlcDataType dataType, string value)
    {
        if (_inovanceEipDriver is not null)
        {
            _inovanceEipDriver.WriteByType(address, dataType, value);
            return;
        }

        if (_beckhoffAdsSymbolDriver is not null)
        {
            switch (dataType)
            {
                case PlcDataType.Int:
                    _beckhoffAdsSymbolDriver.Write(address, PlcTypeConversion.ParseInt16(value));
                    return;
                case PlcDataType.DInt:
                    _beckhoffAdsSymbolDriver.Write(address, PlcTypeConversion.ParseInt32(value));
                    return;
                case PlcDataType.UDInt:
                    _beckhoffAdsSymbolDriver.Write(address, PlcTypeConversion.ParseUInt32(value));
                    return;
                case PlcDataType.Bool:
                    _beckhoffAdsSymbolDriver.Write(address, PlcTypeConversion.ParseBool(value));
                    return;
                case PlcDataType.Real:
                    _beckhoffAdsSymbolDriver.Write(address, PlcTypeConversion.ParseFloat(value));
                    return;
                case PlcDataType.LReal:
                    _beckhoffAdsSymbolDriver.Write(address, PlcTypeConversion.ParseDouble(value));
                    return;
                case PlcDataType.String:
                    _beckhoffAdsSymbolDriver.Write(address, value);
                    return;
                default:
                    throw new NotSupportedException($"不支持写入的数据类型: {dataType}");
            }
        }

        if (_deviceTcpNet is null)
        {
            throw new InvalidOperationException("PLC 客户端未初始化");
        }

        var writeResult = dataType switch
        {
            PlcDataType.Int => _deviceTcpNet.Write(address, PlcTypeConversion.ParseInt16(value)),
            PlcDataType.DInt => _deviceTcpNet.Write(address, PlcTypeConversion.ParseInt32(value)),
            PlcDataType.UDInt => _deviceTcpNet.Write(address, PlcTypeConversion.ParseUInt32(value)),
            PlcDataType.Bool => _deviceTcpNet.Write(address, PlcTypeConversion.ParseBool(value)),
            PlcDataType.Real => _deviceTcpNet.Write(address, PlcTypeConversion.ParseFloat(value)),
            PlcDataType.LReal => _deviceTcpNet.Write(address, PlcTypeConversion.ParseDouble(value)),
            PlcDataType.String => _deviceTcpNet.Write(address, value),
            _ => throw new NotSupportedException($"不支持写入的数据类型: {dataType}")
        };

        EnsureOperateSuccess(writeResult, "写入失败");
    }

    private static string ReadAndConvert<T>(OperateResult<T> result)
    {
        EnsureOperateSuccess(result, "读取失败");
        return PlcTypeConversion.ToInvariantString(result.Content);
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

        throw new BusinessException("PLC 操作失败", details: "重试后仍失败", innerException: lastException);
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
    }
}
