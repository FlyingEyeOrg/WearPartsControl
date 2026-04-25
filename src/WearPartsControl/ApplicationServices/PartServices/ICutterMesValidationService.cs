namespace WearPartsControl.ApplicationServices.PartServices;

public interface ICutterMesValidationService
{
    Task<string> GetExpectedCutterCodeAsync(CutterMesValidationRequest request, CancellationToken cancellationToken = default);
}

public sealed class CutterMesValidationRequest
{
    public string Wsdl { get; init; } = string.Empty;

    public string UserName { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;

    public string Site { get; init; } = string.Empty;

    public string RollNumber { get; init; } = string.Empty;

    public string Parameter { get; init; } = string.Empty;
}