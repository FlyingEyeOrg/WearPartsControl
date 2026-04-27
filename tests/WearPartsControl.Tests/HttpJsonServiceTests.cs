using System;
using System.Net.Http;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using WearPartsControl.ApplicationServices.HttpService;
using WearPartsControl.ApplicationServices.Localization;
using WearPartsControl.ApplicationServices.Localization.Generated;
using WearPartsControl.Exceptions;
using Xunit;

namespace WearPartsControl.Tests;

public sealed class HttpJsonServiceTests
{
    [Fact]
    public async Task SendAsync_WhenHttpFailed_ShouldThrowUserFriendlyException()
    {
        var service = new HttpJsonService(
            new StubHttpRequestService(new HttpRawResponse(400, "Bad Request", "Bad Request Payload")),
            new StubLocalizationService());

        await Assert.ThrowsAsync<UserFriendlyException>(async () =>
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "https://example.com/test");
            await service.SendAsync<TestDto>(request);
        });
    }

    [Fact]
    public async Task SendAsync_WhenHttpSucceeded_ShouldDeserializeJsonBody()
    {
        var service = new HttpJsonService(
            new StubHttpRequestService(new HttpRawResponse(200, "OK", "{\"name\":\"demo\"}")),
            new StubLocalizationService());

        using var request = new HttpRequestMessage(HttpMethod.Get, "https://example.com/test");
        var result = await service.SendAsync<TestDto>(request);

        Assert.Equal("demo", result.Name);
    }

    private sealed class TestDto
    {
        public string Name { get; set; } = string.Empty;
    }

    private sealed class StubHttpRequestService : IHttpRequestService
    {
        private readonly HttpRawResponse _response;

        public StubHttpRequestService(HttpRawResponse response)
        {
            _response = response;
        }

        public ValueTask<HttpRawResponse> SendAsync(HttpRequestMessage request, HttpRequestExecutionOptions? options = null, CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(_response);
        }
    }

    private sealed class StubLocalizationService : ILocalizationService
    {
        public string this[string name] => name;

        public LocalizationCatalog Catalog { get; } = new(static key => key);

        public ValueTask InitializeAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public ValueTask SetCultureAsync(string cultureName, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public CultureInfo CurrentCulture { get; } = CultureInfo.InvariantCulture;
    }
}
