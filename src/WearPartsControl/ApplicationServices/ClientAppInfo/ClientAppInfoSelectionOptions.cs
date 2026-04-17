namespace WearPartsControl.ApplicationServices.ClientAppInfo;

public sealed class ClientAppInfoSelectionOptions
{
    public IReadOnlyList<string> AreaOptions { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> ProcedureOptions { get; init; } = Array.Empty<string>();
}
