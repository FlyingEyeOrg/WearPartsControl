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

    public required string CurrentValueText { get; set; }

    public required string WarningValueText { get; set; }

    public required string ShutdownValueText { get; set; }

    public required double CurrentValue { get; set; }

    public required double WarningValue { get; set; }

    public required double ShutdownValue { get; set; }

    public required double InstalledCurrentValue { get; init; }

    public required double InstalledWarningValue { get; init; }

    public required double InstalledShutdownValue { get; init; }

    public WearPartReplacementRecordEntity? LatestRecord { get; init; }

    public WearPartReplacementRecordEntity? LatestRemovalRecord { get; set; }

    public double PlcWriteValue { get; set; }
}