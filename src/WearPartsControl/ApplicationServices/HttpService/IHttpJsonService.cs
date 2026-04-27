using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace WearPartsControl.ApplicationServices.HttpService;

public interface IHttpJsonService
{
    ValueTask<TResponse> GetAsync<TResponse>(string requestUri, CancellationToken cancellationToken = default);

    ValueTask<TResponse> PostAsync<TRequest, TResponse>(string requestUri, TRequest requestBody, CancellationToken cancellationToken = default);

    ValueTask<TResponse> SendAsync<TResponse>(HttpRequestMessage request, CancellationToken cancellationToken = default);
}
