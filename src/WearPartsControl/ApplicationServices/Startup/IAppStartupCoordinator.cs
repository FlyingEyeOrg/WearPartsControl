namespace WearPartsControl.ApplicationServices.Startup;

public interface IAppStartupCoordinator
{
    Task EnsureInitializedAsync(CancellationToken cancellationToken = default);
}