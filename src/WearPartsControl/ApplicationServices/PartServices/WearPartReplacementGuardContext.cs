using WearPartsControl.ApplicationServices.LoginService;
using WearPartsControl.Domain.Entities;

namespace WearPartsControl.ApplicationServices.PartServices;

public sealed class WearPartReplacementGuardContext
{
    public required WearPartReplacementRequest Request { get; init; }

    public required MhrUser CurrentUser { get; init; }

    public required ClientAppConfigurationEntity ClientAppConfiguration { get; init; }

    public required WearPartDefinitionEntity Definition { get; init; }

    public required string NormalizedBarcode { get; init; }

    public required string NormalizedReason { get; init; }

    public required string CurrentValueText { get; init; }

    public required string WarningValueText { get; init; }

    public required string ShutdownValueText { get; init; }

    public required double CurrentValue { get; init; }

    public required double WarningValue { get; init; }

    public required double ShutdownValue { get; init; }

    public WearPartReplacementRecordEntity? LatestRecord { get; init; }

    public WearPartReplacementRecordEntity? LatestRemovalRecord { get; set; }

    public double PlcWriteValue { get; set; }
}