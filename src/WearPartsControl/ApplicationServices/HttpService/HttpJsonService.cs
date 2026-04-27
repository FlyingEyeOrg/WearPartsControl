using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
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
    private readonly ILogger<HttpJsonService> _logger;

    public HttpJsonService(IHttpRequestService httpRequestService, ILocalizationService localizationService, ILogger<HttpJsonService> logger)
    {
        _httpRequestService = httpRequestService;
        _localizationService = localizationService;
        _logger = logger;
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
        ArgumentNullException.ThrowIfNull(request);
        var method = request.Method?.Method ?? "UNKNOWN";
        var uri = request.RequestUri?.ToString() ?? "UNKNOWN";
        var responseType = typeof(TResponse).Name;

        _logger.LogDebug("JSON HTTP request started: {Method} {Uri} -> {ResponseType}", method, uri, responseType);
        var response = await _httpRequestService.SendAsync(request, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("JSON HTTP request failed: {Method} {Uri} {StatusCode} -> {ResponseType}", method, uri, response.StatusCode, responseType);
            var message = string.IsNullOrWhiteSpace(response.Body)
                ? string.Format(
                    L("HttpService.HttpRequestFailed"),
                    response.StatusCode,
                    response.ReasonPhrase ?? string.Empty)
                : response.Body;

            throw new UserFriendlyException(message, code: $"Http:{response.StatusCode}");
        }

        TResponse? payload;
        try
        {
            payload = JsonSerializer.Deserialize<TResponse>(response.Body, JsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "JSON HTTP response deserialization failed: {Method} {Uri} -> {ResponseType}; body length {BodyLength}", method, uri, responseType, response.Body?.Length ?? 0);
            throw;
        }

        if (payload is null)
        {
            _logger.LogWarning("JSON HTTP response payload is empty: {Method} {Uri} -> {ResponseType}", method, uri, responseType);
            throw new UserFriendlyException(L("HttpService.EmptyPayload"), code: "Http:EmptyPayload");
        }

        _logger.LogDebug("JSON HTTP request succeeded: {Method} {Uri} -> {ResponseType}", method, uri, responseType);
        return payload;
    }

    private string L(string key) => _localizationService[key];
}
