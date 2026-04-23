using WearPartsControl.ApplicationServices.Localization;
using WearPartsControl.Exceptions;

namespace WearPartsControl.ApplicationServices.PartServices;

public sealed class LifetimeReachedReplacementGuard : IWearPartReplacementGuard
{
    public int Order => 300;

    public Task ValidateAsync(WearPartReplacementGuardContext context, CancellationToken cancellationToken = default)
    {
        if (WearPartReplacementReason.RequiresWarningLifetime(context.NormalizedReason)
            && context.CurrentValue < context.WarningValue)
        {
            throw new UserFriendlyException(LocalizedText.Get("Services.WearPartReplacement.LifetimeNotReached"), code: "WearPartReplacement:LifetimeNotReached");
        }

        if (WearPartReplacementReason.RequiresBelowShutdownLifetime(context.NormalizedReason)
            && context.CurrentValue >= context.ShutdownValue)
        {
            throw new UserFriendlyException(
                LocalizedText.Format(
                    "Services.WearPartReplacement.WarningBeforeShutdownWindowRequired",
                    WearPartReplacementReason.GetDisplayName(context.NormalizedReason)),
                code: "WearPartReplacement:WarningBeforeShutdownWindowRequired");
        }

        return Task.CompletedTask;
    }
}