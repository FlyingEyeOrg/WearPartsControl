namespace WearPartsControl.ApplicationServices.PlcService;

public interface IPlcOperationPipeline
{
    Task ExecuteAsync(string operationName, Action<IPlcOperationContext> operation, CancellationToken cancellationToken = default);

    Task<TResult> ExecuteAsync<TResult>(string operationName, Func<IPlcOperationContext, TResult> operation, CancellationToken cancellationToken = default);

    Task ExecuteAsync(string operationName, Func<IPlcOperationContext, Task> operation, CancellationToken cancellationToken = default);

    Task<TResult> ExecuteAsync<TResult>(string operationName, Func<IPlcOperationContext, Task<TResult>> operation, CancellationToken cancellationToken = default);
}