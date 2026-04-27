namespace WearPartsControl.ApplicationServices.PlcService;

// 底层 PLC 驱动接口，仅供 PlcOperationPipeline 使用。
internal interface IPlcService
{
    bool IsConnected { get; }

    Task ConnectAsync(PlcConnectionOptions options, bool forceReconnect = false, CancellationToken cancellationToken = default);

    void Disconnect();

    TValue Read<TValue>(string address, int retryCount = 1);

    void Write<TValue>(string address, TValue value, int retryCount = 1);
}
