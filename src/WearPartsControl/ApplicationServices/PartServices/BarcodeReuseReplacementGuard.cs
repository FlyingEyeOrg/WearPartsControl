using WearPartsControl.Domain.Repositories;
using WearPartsControl.Exceptions;

namespace WearPartsControl.ApplicationServices.PartServices;

public sealed class BarcodeReuseReplacementGuard : IWearPartReplacementGuard
{
    private readonly IWearPartReplacementRecordRepository _replacementRecordRepository;

    public BarcodeReuseReplacementGuard(IWearPartReplacementRecordRepository replacementRecordRepository)
    {
        _replacementRecordRepository = replacementRecordRepository;
    }

    public int Order => 200;

    public async Task ValidateAsync(WearPartReplacementGuardContext context, CancellationToken cancellationToken = default)
    {
        var barcodeAlreadyUsed = await _replacementRecordRepository
            .ExistsNewBarcodeAsync(context.Definition.Id, context.NormalizedBarcode, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (!barcodeAlreadyUsed)
        {
            return;
        }

        if (string.Equals(context.NormalizedReason, WearPartReplacementReason.ChangePosition, StringComparison.Ordinal)
            && string.Equals(context.LatestRecord?.NewBarcode, context.NormalizedBarcode, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var latestRemovalRecord = await _replacementRecordRepository
            .GetLatestByOldBarcodeAsync(context.Definition.Id, context.NormalizedBarcode, cancellationToken)
            .ConfigureAwait(false);

        if (latestRemovalRecord is null)
        {
            throw new UserFriendlyException(
                $"易损件 {context.Definition.PartName} 已存在条码 {context.NormalizedBarcode} 的更换记录，不允许重复使用。",
                code: "WearPartReplacement:BarcodeDuplicated");
        }

        if (!WearPartReplacementReason.AllowsBarcodeReuseAfterRemoval(latestRemovalRecord.ReplacementReason))
        {
            throw new UserFriendlyException("条码重复，请更换其他条码。", code: "WearPartReplacement:BarcodeDuplicated");
        }

        context.LatestRemovalRecord = latestRemovalRecord;
        context.PlcWriteValue = WearPartReplacementValueParser.ParseDouble(
            latestRemovalRecord.CurrentValue,
            context.Definition.CurrentValueDataType,
            context.Definition.CurrentValueAddress);
    }
}