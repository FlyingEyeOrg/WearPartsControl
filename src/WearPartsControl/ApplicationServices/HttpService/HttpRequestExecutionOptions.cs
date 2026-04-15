namespace WearPartsControl.ApplicationServices.HttpService;

public sealed record HttpRequestExecutionOptions
{
    public int? TimeoutMilliseconds { get; init; }

    public bool IgnoreServerCertificateErrors { get; init; }
}
