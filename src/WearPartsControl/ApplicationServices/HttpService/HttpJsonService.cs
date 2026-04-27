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

    private readonly IHttpRequestService _httpRequestService;
    private readonly ILocalizationService _localizationService;

    public HttpJsonService(IHttpRequestService httpRequestService, ILocalizationService localizationService)
    {
        _httpRequestService = httpRequestService;
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
        var response = await _httpRequestService.SendAsync(request, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var message = string.IsNullOrWhiteSpace(response.Body)
                ? string.Format(
                    L("HttpService.HttpRequestFailed"),
                    response.StatusCode,
                    response.ReasonPhrase ?? string.Empty)
                : response.Body;

            throw new UserFriendlyException(message, code: $"Http:{response.StatusCode}");
        }

        var payload = JsonSerializer.Deserialize<TResponse>(response.Body, JsonOptions);
        if (payload is null)
        {
            throw new UserFriendlyException(L("HttpService.EmptyPayload"), code: "Http:EmptyPayload");
        }

        return payload;
    }

    private string L(string key) => _localizationService[key];
}
