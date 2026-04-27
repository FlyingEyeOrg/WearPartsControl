namespace WearPartsControl.ApplicationServices.AutoStart;

public interface IAutoStartService
{
    ValueTask<bool> IsEnabledAsync(CancellationToken cancellationToken = default);

    ValueTask SetEnabledAsync(bool enabled, CancellationToken cancellationToken = default);
}