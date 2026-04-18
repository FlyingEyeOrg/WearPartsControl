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
            throw new UserFriendlyException("最近一次无数据记录。", code: "WearPartReplacement:LatestRecordMissing");
        }

        if (!string.Equals(context.LatestRecord.NewBarcode, context.NormalizedBarcode, StringComparison.OrdinalIgnoreCase))
        {
            throw new UserFriendlyException("最近一条数据的新条码跟当前更换的条码不一致，请确认。", code: "WearPartReplacement:ChangePositionBarcodeMismatch");
        }

        if (context.CurrentUser.AccessLevel < 3)
        {
            throw new AuthorizationException("当前扫的条码跟上次使用的条码一致，请 ME 登录确认是否继续使用。", code: "WearPartReplacement:ChangePositionRequiresMe");
        }

        return Task.CompletedTask;
    }
}