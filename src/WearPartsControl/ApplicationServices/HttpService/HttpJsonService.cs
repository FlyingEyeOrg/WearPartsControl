using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using WearPartsControl.Exceptions;

namespace WearPartsControl.ApplicationServices.HttpService;

public sealed class HttpJsonService : IHttpJsonService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;

    public HttpJsonService(HttpClient httpClient)
    {
        _httpClient = httpClient;
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
                ? $"HTTP request failed: {(int)response.StatusCode} {response.ReasonPhrase}"
                : body;

            throw new UserFriendlyException(message, code: $"Http:{(int)response.StatusCode}");
        }

        var payload = await response.Content.ReadFromJsonAsync<TResponse>(JsonOptions, cancellationToken).ConfigureAwait(false);
        if (payload is null)
        {
            throw new UserFriendlyException("HTTP response is empty or JSON deserialization failed.", code: "Http:EmptyPayload");
        }

        return payload;
    }
}
