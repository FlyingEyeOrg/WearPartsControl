using System.Net.Http;
using WearPartsControl.ApplicationServices.Localization;
using WearPartsControl.Exceptions;

namespace WearPartsControl.ApplicationServices.HttpService;

public sealed class HttpRequestService : IHttpRequestService
{
    private readonly HttpClient _httpClient;
    private readonly ILocalizationService _localizationService;

    public HttpRequestService(HttpClient httpClient, ILocalizationService localizationService)
    {
        _httpClient = httpClient;
        _localizationService = localizationService;
    }

    public async ValueTask<HttpRawResponse> SendAsync(HttpRequestMessage request, HttpRequestExecutionOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (options?.TimeoutMilliseconds is int timeoutMs && timeoutMs <= 0)
        {
            throw new UserFriendlyException(L("HttpService.InvalidTimeout"));
        }

        if (options?.IgnoreServerCertificateErrors == true)
        {
            return await SendWithTemporaryClientAsync(request, options, cancellationToken).ConfigureAwait(false);
        }

        using var timeoutCancellationTokenSource = CreateTimeoutCancellationTokenSource(options, cancellationToken);
        var effectiveCancellationToken = timeoutCancellationTokenSource?.Token ?? cancellationToken;

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, effectiveCancellationToken).ConfigureAwait(false);
        var body = response.Content is null
            ? string.Empty
            : await response.Content.ReadAsStringAsync(effectiveCancellationToken).ConfigureAwait(false);

        return new HttpRawResponse((int)response.StatusCode, response.ReasonPhrase, body);
    }

    private static CancellationTokenSource? CreateTimeoutCancellationTokenSource(HttpRequestExecutionOptions? options, CancellationToken cancellationToken)
    {
        if (options?.TimeoutMilliseconds is not int timeoutMs)
        {
            return null;
        }

        var cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cancellationTokenSource.CancelAfter(TimeSpan.FromMilliseconds(timeoutMs));
        return cancellationTokenSource;
    }

    private static async ValueTask<HttpRawResponse> SendWithTemporaryClientAsync(HttpRequestMessage request, HttpRequestExecutionOptions? options, CancellationToken cancellationToken)
    {
        var handler = new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(10),
            SslOptions =
            {
                RemoteCertificateValidationCallback = static (_, _, _, _) => true
            }
        };

        using var client = new HttpClient(handler);
        using var timeoutCancellationTokenSource = CreateTimeoutCancellationTokenSource(options, cancellationToken);
        var effectiveCancellationToken = timeoutCancellationTokenSource?.Token ?? cancellationToken;

        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, effectiveCancellationToken).ConfigureAwait(false);
        var body = response.Content is null
            ? string.Empty
            : await response.Content.ReadAsStringAsync(effectiveCancellationToken).ConfigureAwait(false);

        return new HttpRawResponse((int)response.StatusCode, response.ReasonPhrase, body);
    }

    private string L(string key) => _localizationService[key];
}