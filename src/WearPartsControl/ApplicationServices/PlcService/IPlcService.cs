namespace WearPartsControl.ApplicationServices.PlcService;

public interface IPlcService
{
    bool IsConnected { get; }

    void Connect(PlcConnectionOptions options);

    void Disconnect();

    string ReadAsString(string address, PlcDataType dataType, int retryCount = 1);

    void WriteFromString(string address, PlcDataType dataType, string value, int retryCount = 1);
}
