namespace WearPartsControl.ApplicationServices.PartServices;

public interface IWearPartReplacementGuard
{
    int Order { get; }

    Task ValidateAsync(WearPartReplacementGuardContext context, CancellationToken cancellationToken = default);
}