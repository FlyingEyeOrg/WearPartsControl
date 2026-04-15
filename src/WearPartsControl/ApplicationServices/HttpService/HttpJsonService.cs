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

        HttpClient? localClient = null;
        HttpClient clientToUse = _httpClient;

        if (options is { IgnoreServerCertificateErrors: true } || options?.TimeoutMilliseconds is not null)
        {
            var handler = new HttpClientHandler();
            if (options?.IgnoreServerCertificateErrors == true)
            {
                handler.ServerCertificateCustomValidationCallback = static (_, _, _, _) => true;
            }

            localClient = new HttpClient(handler);
            if (options?.TimeoutMilliseconds is int timeoutMs)
            {
                if (timeoutMs <= 0)
                {
                    throw new UserFriendlyException(L("HttpService.InvalidTimeout"));
                }

                localClient.Timeout = TimeSpan.FromMilliseconds(timeoutMs);
            }

            clientToUse = localClient;
        }

        try
        {
            using var response = await clientToUse.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            var body = response.Content is null
                ? string.Empty
                : await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            return new HttpRawResponse((int)response.StatusCode, response.ReasonPhrase, body);
        }
        finally
        {
            localClient?.Dispose();
        }
    }

    private string L(string key) => _localizationService[key];
}
