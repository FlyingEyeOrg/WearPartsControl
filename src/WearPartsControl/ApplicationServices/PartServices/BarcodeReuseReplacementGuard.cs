using WearPartsControl.ApplicationServices.Localization;
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
            .GetLatestByCurrentBarcodeAsync(context.Definition.Id, context.NormalizedBarcode, cancellationToken)
            .ConfigureAwait(false);

        if (latestRemovalRecord is null)
        {
            throw new UserFriendlyException(
                LocalizedText.Format("Services.WearPartReplacement.BarcodeAlreadyUsed", context.Definition.PartName, context.NormalizedBarcode),
                code: "WearPartReplacement:BarcodeDuplicated");
        }

        if (string.Equals(WearPartReplacementReason.NormalizeCode(latestRemovalRecord.ReplacementReason), WearPartReplacementReason.Normal, StringComparison.Ordinal))
        {
            throw new UserFriendlyException(
                LocalizedText.Format("Services.WearPartReplacement.ReusedPartBlockedAfterNormalReplacement", context.NormalizedBarcode),
                code: "WearPartReplacement:ReusedPartBlockedAfterNormalReplacement");
        }

        context.LatestRemovalRecord = latestRemovalRecord;
        context.CurrentValueText = latestRemovalRecord.CurrentValue;
        context.WarningValueText = latestRemovalRecord.WarningValue;
        context.ShutdownValueText = latestRemovalRecord.ShutdownValue;
        context.PlcWriteValue = WearPartReplacementValueParser.ParseDouble(
            latestRemovalRecord.CurrentValue,
            context.Definition.CurrentValueDataType,
            context.Definition.CurrentValueAddress);
        context.CurrentValue = context.PlcWriteValue;
        context.WarningValue = WearPartReplacementValueParser.ParseDouble(
            latestRemovalRecord.WarningValue,
            context.Definition.WarningValueDataType,
            context.Definition.WarningValueAddress);
        context.ShutdownValue = WearPartReplacementValueParser.ParseDouble(
            latestRemovalRecord.ShutdownValue,
            context.Definition.ShutdownValueDataType,
            context.Definition.ShutdownValueAddress);

        if (context.CurrentValue >= context.ShutdownValue)
        {
            throw new UserFriendlyException(LocalizedText.Get("Services.WearPartReplacement.ReusedPartReachedShutdownLifetime"), code: "WearPartReplacement:ReusedPartReachedShutdownLifetime");
        }
    }
}