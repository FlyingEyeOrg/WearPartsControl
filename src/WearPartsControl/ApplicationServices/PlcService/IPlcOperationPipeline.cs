namespace WearPartsControl.ApplicationServices.PlcService;

public interface IPlcOperationPipeline
{
    Task ConnectAsync(string operationName, PlcConnectionOptions options, CancellationToken cancellationToken = default);

    Task DisconnectAsync(string operationName, CancellationToken cancellationToken = default);

    Task<bool> IsConnectedAsync(string operationName, CancellationToken cancellationToken = default);

    Task<TValue> ReadAsync<TValue>(string operationName, string address, int retryCount = 1, CancellationToken cancellationToken = default);

    Task WriteAsync<TValue>(string operationName, string address, TValue value, int retryCount = 1, CancellationToken cancellationToken = default);
}