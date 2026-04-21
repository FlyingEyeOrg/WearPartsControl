using WearPartsControl.ApplicationServices.Localization;
using WearPartsControl.Exceptions;

namespace WearPartsControl.ApplicationServices.PartServices;

public sealed class ChangePositionReplacementGuard : IWearPartReplacementGuard
{
    public int Order => 400;

    public Task ValidateAsync(WearPartReplacementGuardContext context, CancellationToken cancellationToken = default)
    {
        if (!string.Equals(context.NormalizedReason, WearPartReplacementReason.ChangePosition, StringComparison.Ordinal))
        {
            return Task.CompletedTask;
        }

        if (context.LatestRecord is null)
        {
            throw new UserFriendlyException(LocalizedText.Get("Services.WearPartReplacement.LatestRecordMissing"), code: "WearPartReplacement:LatestRecordMissing");
        }

        if (!string.Equals(context.LatestRecord.NewBarcode, context.NormalizedBarcode, StringComparison.OrdinalIgnoreCase))
        {
            throw new UserFriendlyException(LocalizedText.Get("Services.WearPartReplacement.ChangePositionBarcodeMismatch"), code: "WearPartReplacement:ChangePositionBarcodeMismatch");
        }

        if (context.CurrentUser.AccessLevel < 3)
        {
            throw new AuthorizationException(LocalizedText.Get("Services.WearPartReplacement.ChangePositionRequiresMe"), code: "WearPartReplacement:ChangePositionRequiresMe");
        }

        return Task.CompletedTask;
    }
}