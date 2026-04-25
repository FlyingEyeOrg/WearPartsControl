namespace WearPartsControl.ApplicationServices.Startup;

public interface IStartupPlcWarmupService
{
    Task WarmupAsync(Func<string, Task>? reportLoadingAsync = null, CancellationToken cancellationToken = default);
}