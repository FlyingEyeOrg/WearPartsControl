namespace WearPartsControl.ApplicationServices.HttpService;

public sealed record HttpRawResponse(int StatusCode, string? ReasonPhrase, string Body)
{
    public bool IsSuccessStatusCode => StatusCode is >= 200 and <= 299;
}
