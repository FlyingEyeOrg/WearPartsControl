namespace WearPartsControl.ApplicationServices.PartServices;

public interface ICutterMesValidationService
{
    Task<CutterMesValidationSnapshot> GetValidationSnapshotAsync(CutterMesValidationRequest request, CancellationToken cancellationToken = default);

    Task<string> GetExpectedCutterCodeAsync(CutterMesValidationRequest request, CancellationToken cancellationToken = default);
}