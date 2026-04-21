namespace WearPartsControl.ApplicationServices.PlcService;

public sealed record PlcConnectionOptions
{
    public PlcProtocolType PlcType { get; init; }

    public string IpAddress { get; init; } = string.Empty;

    public int Port { get; init; }

    public int SiemensRack { get; init; }

    public int SiemensSlot { get; init; }

    public bool IsStringReverse { get; init; } = true;

    public string? HostIpAddress { get; init; }

    public string? BeckhoffAmsNetId { get; init; }

    public int BeckhoffAmsPort { get; init; } = 851;

    public int ConnectTimeoutMilliseconds { get; init; } = 5000;

    public int StringReadLength { get; init; } = 99;
}
