namespace WearPartsControl.ApplicationServices.Startup;

public interface IAppStartupCoordinator
{
    Task EnsureInitializedAsync(Func<string, Task>? reportLoadingAsync = null, CancellationToken cancellationToken = default);
}