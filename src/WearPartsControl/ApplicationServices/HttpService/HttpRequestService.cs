using System.Net.Http;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using WearPartsControl.ApplicationServices.Localization;
using WearPartsControl.Exceptions;

namespace WearPartsControl.ApplicationServices.HttpService;

public sealed class HttpRequestService : IHttpRequestService
{
    private readonly HttpClient _httpClient;
    private readonly ILocalizationService _localizationService;
    private readonly ILogger<HttpRequestService> _logger;

    public HttpRequestService(HttpClient httpClient, ILocalizationService localizationService, ILogger<HttpRequestService> logger)
    {
        _httpClient = httpClient;
        _localizationService = localizationService;
        _logger = logger;
    }

    public async ValueTask<HttpRawResponse> SendAsync(HttpRequestMessage request, HttpRequestExecutionOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var method = request.Method?.Method ?? "UNKNOWN";
        var uri = request.RequestUri?.ToString() ?? "UNKNOWN";

        if (options?.TimeoutMilliseconds is int timeoutMs && timeoutMs <= 0)
        {
            throw new UserFriendlyException(L("HttpService.InvalidTimeout"));
        }

        if (options?.IgnoreServerCertificateErrors == true)
        {
            _logger.LogWarning("HTTP request disables server certificate validation: {Method} {Uri}", method, uri);
            return await SendWithTemporaryClientAsync(request, options, _logger, cancellationToken).ConfigureAwait(false);
        }

        using var timeoutCancellationTokenSource = CreateTimeoutCancellationTokenSource(options, cancellationToken);
        var effectiveCancellationToken = timeoutCancellationTokenSource?.Token ?? cancellationToken;
        var stopwatch = Stopwatch.StartNew();

        try
        {
            _logger.LogDebug("HTTP request started: {Method} {Uri}", method, uri);
            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, effectiveCancellationToken).ConfigureAwait(false);
            var body = response.Content is null
                ? string.Empty
                : await response.Content.ReadAsStringAsync(effectiveCancellationToken).ConfigureAwait(false);

            _logger.Log(
                response.IsSuccessStatusCode ? LogLevel.Debug : LogLevel.Warning,
                "HTTP request completed: {Method} {Uri} {StatusCode} in {ElapsedMilliseconds} ms",
                method,
                uri,
                (int)response.StatusCode,
                stopwatch.ElapsedMilliseconds);

            return new HttpRawResponse((int)response.StatusCode, response.ReasonPhrase, body);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "HTTP request timed out: {Method} {Uri} after {ElapsedMilliseconds} ms", method, uri, stopwatch.ElapsedMilliseconds);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "HTTP request failed: {Method} {Uri} after {ElapsedMilliseconds} ms", method, uri, stopwatch.ElapsedMilliseconds);
            throw;
        }
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

    private static async ValueTask<HttpRawResponse> SendWithTemporaryClientAsync(HttpRequestMessage request, HttpRequestExecutionOptions? options, ILogger logger, CancellationToken cancellationToken)
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
        var method = request.Method?.Method ?? "UNKNOWN";
        var uri = request.RequestUri?.ToString() ?? "UNKNOWN";
        var stopwatch = Stopwatch.StartNew();

        try
        {
            logger.LogDebug("HTTP request started with temporary client: {Method} {Uri}", method, uri);
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, effectiveCancellationToken).ConfigureAwait(false);
            var body = response.Content is null
                ? string.Empty
                : await response.Content.ReadAsStringAsync(effectiveCancellationToken).ConfigureAwait(false);

            logger.Log(
                response.IsSuccessStatusCode ? LogLevel.Debug : LogLevel.Warning,
                "HTTP request completed with temporary client: {Method} {Uri} {StatusCode} in {ElapsedMilliseconds} ms",
                method,
                uri,
                (int)response.StatusCode,
                stopwatch.ElapsedMilliseconds);

            return new HttpRawResponse((int)response.StatusCode, response.ReasonPhrase, body);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogError(ex, "HTTP request timed out with temporary client: {Method} {Uri} after {ElapsedMilliseconds} ms", method, uri, stopwatch.ElapsedMilliseconds);
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "HTTP request failed with temporary client: {Method} {Uri} after {ElapsedMilliseconds} ms", method, uri, stopwatch.ElapsedMilliseconds);
            throw;
        }
    }

    private string L(string key) => _localizationService[key];
}