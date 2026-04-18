using WearPartsControl.Exceptions;

namespace WearPartsControl.ApplicationServices.PartServices;

public sealed class LifetimeReachedReplacementGuard : IWearPartReplacementGuard
{
    public int Order => 300;

    public Task ValidateAsync(WearPartReplacementGuardContext context, CancellationToken cancellationToken = default)
    {
        if (!WearPartReplacementReason.RequiresLifetimeReached(context.NormalizedReason))
        {
            return Task.CompletedTask;
        }

        if (context.CurrentValue < context.ShutdownValue)
        {
            throw new UserFriendlyException("检测到当前易损件未达到停机寿命，不允许更换。", code: "WearPartReplacement:LifetimeNotReached");
        }

        return Task.CompletedTask;
    }
}