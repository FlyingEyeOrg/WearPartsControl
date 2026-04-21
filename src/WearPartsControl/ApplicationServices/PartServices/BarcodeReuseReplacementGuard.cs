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
            .GetLatestByOldBarcodeAsync(context.Definition.Id, context.NormalizedBarcode, cancellationToken)
            .ConfigureAwait(false);

        if (latestRemovalRecord is null)
        {
            throw new UserFriendlyException(
                LocalizedText.Format("Services.WearPartReplacement.BarcodeAlreadyUsed", context.Definition.PartName, context.NormalizedBarcode),
                code: "WearPartReplacement:BarcodeDuplicated");
        }

        if (!WearPartReplacementReason.AllowsBarcodeReuseAfterRemoval(latestRemovalRecord.ReplacementReason))
        {
            throw new UserFriendlyException(LocalizedText.Get("Services.WearPartReplacement.BarcodeDuplicated"), code: "WearPartReplacement:BarcodeDuplicated");
        }

        context.LatestRemovalRecord = latestRemovalRecord;
        context.PlcWriteValue = WearPartReplacementValueParser.ParseDouble(
            latestRemovalRecord.CurrentValue,
            context.Definition.CurrentValueDataType,
            context.Definition.CurrentValueAddress);
    }
}