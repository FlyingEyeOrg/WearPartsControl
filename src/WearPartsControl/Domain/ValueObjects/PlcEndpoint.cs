using WearPartsControl.Domain.Exceptions;

namespace WearPartsControl.Domain.ValueObjects;

public readonly record struct PlcEndpoint
{
    public PlcEndpoint(string ipAddress, int port)
    {
        if (string.IsNullOrWhiteSpace(ipAddress))
        {
            throw new DomainValidationException("PLC IP 地址不能为空。");
        }

        if (port <= 0 || port > 65535)
        {
            throw new DomainValidationException("PLC 端口号必须在 1 到 65535 之间。");
        }

        IpAddress = ipAddress.Trim();
        Port = port;
    }

    public string IpAddress { get; }

    public int Port { get; }
}
