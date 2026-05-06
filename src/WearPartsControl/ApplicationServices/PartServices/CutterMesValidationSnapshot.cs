namespace WearPartsControl.ApplicationServices.PartServices;

public sealed class CutterMesValidationSnapshot
{
    public string ExpectedCutterCode { get; init; } = string.Empty;

    public string KdlText { get; init; } = string.Empty;

    public double? KdlValue { get; init; }
}