using System;
using System.Net;
using System.Net.Http;
using System.Text;
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
        using var httpClient = new HttpClient(new StubHandler(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("Bad Request Payload", Encoding.UTF8, "application/json")
            })));

        var service = new HttpJsonService(httpClient, new StubLocalizationService());

        await Assert.ThrowsAsync<UserFriendlyException>(async () =>
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "https://example.com/test");
            await service.SendAsync<TestDto>(request);
        });
    }

    [Fact]
    public async Task SendRawAsync_WhenTimeoutMillisecondsInvalid_ShouldThrowUserFriendlyException()
    {
        using var httpClient = new HttpClient(new StubHandler(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json")
            })));

        var service = new HttpJsonService(httpClient, new StubLocalizationService());

        var exception = await Assert.ThrowsAsync<UserFriendlyException>(async () =>
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "https://example.com/test");
            await service.SendRawAsync(request, new HttpRequestExecutionOptions { TimeoutMilliseconds = 0 });
        });

        Assert.Equal("HttpService.InvalidTimeout", exception.Message);
    }

    private sealed class TestDto
    {
        public string Name { get; set; } = string.Empty;
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _handler;

        public StubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return _handler(request);
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
