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

    private string L(string key) => _localizationService[key];
}
