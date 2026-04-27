namespace WearPartsControl.ApplicationServices.PartServices;

public interface ICutterMesValidationService
{
    Task<string> GetExpectedCutterCodeAsync(CutterMesValidationRequest request, CancellationToken cancellationToken = default);
}