using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using WearPartsControl.ApplicationServices.Localization;
using WearPartsControl.Exceptions;

namespace WearPartsControl.ApplicationServices.HttpService;

public sealed class HttpJsonService : IHttpJsonService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly ILocalizationService _localizationService;

    public HttpJsonService(HttpClient httpClient, ILocalizationService localizationService)
    {
        _httpClient = httpClient;
        _localizationService = localizationService;
    }

    public async ValueTask<TResponse> GetAsync<TResponse>(string requestUri, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        return await SendAsync<TResponse>(request, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<TResponse> PostAsync<TRequest, TResponse>(string requestUri, TRequest requestBody, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, requestUri)
        {
            Content = JsonContent.Create(requestBody, options: JsonOptions)
        };

        return await SendAsync<TResponse>(request, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<TResponse> SendAsync<TResponse>(HttpRequestMessage request, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var body = response.Content is null
                ? string.Empty
                : await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            var message = string.IsNullOrWhiteSpace(body)
                ? string.Format(
                    L("HttpService.HttpRequestFailed"),
                    (int)response.StatusCode,
                    response.ReasonPhrase ?? string.Empty)
                : body;

            throw new UserFriendlyException(message, code: $"Http:{(int)response.StatusCode}");
        }

        var payload = await response.Content.ReadFromJsonAsync<TResponse>(JsonOptions, cancellationToken).ConfigureAwait(false);
        if (payload is null)
        {
            throw new UserFriendlyException(L("HttpService.EmptyPayload"), code: "Http:EmptyPayload");
        }

        return payload;
    }

    public async ValueTask<HttpRawResponse> SendRawAsync(HttpRequestMessage request, HttpRequestExecutionOptions? options = null, CancellationToken cancellationToken = default)
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
