using System.Net.Http;

namespace WearPartsControl.ApplicationServices.HttpService;

public interface IHttpRequestService
{
    ValueTask<HttpRawResponse> SendAsync(HttpRequestMessage request, HttpRequestExecutionOptions? options = null, CancellationToken cancellationToken = default);
}