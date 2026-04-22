using WearPartsControl.ApplicationServices.Localization;
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

        if (context.CurrentValue < context.WarningValue)
        {
            throw new UserFriendlyException(LocalizedText.Get("Services.WearPartReplacement.LifetimeNotReached"), code: "WearPartReplacement:LifetimeNotReached");
        }

        return Task.CompletedTask;
    }
}