namespace WearPartsControl.ApplicationServices.PlcService;

public interface IPlcService
{
    bool IsConnected { get; }

    void Connect(PlcConnectionOptions options);

    void Disconnect();

    TValue Read<TValue>(string address, int retryCount = 1);

    void Write<TValue>(string address, TValue value, int retryCount = 1);
}
