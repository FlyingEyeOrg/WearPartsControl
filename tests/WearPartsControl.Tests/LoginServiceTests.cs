using System.Globalization;
using System.Net.Http;
using WearPartsControl.ApplicationServices;
using WearPartsControl.ApplicationServices.HttpService;
using WearPartsControl.ApplicationServices.Localization;
using WearPartsControl.ApplicationServices.Localization.Generated;
using WearPartsControl.ApplicationServices.LoginService;
using Xunit;

namespace WearPartsControl.Tests;

public sealed class LoginServiceTests
{
    [Fact]
    public async Task LogoutAsync_ShouldClearCurrentUser()
    {
        var currentUserAccessor = new CurrentUserAccessor();
        currentUserAccessor.SetCurrentUser(new MhrUser
        {
            CardId = "CARD-01",
            WorkId = "WORK-01",
            AccessLevel = 2
        });

        var service = new LoginService(new StubHttpJsonService(), new StubLocalizationService(), currentUserAccessor);

        await service.LogoutAsync();

        Assert.Null(service.GetCurrentUser());
    }

    private sealed class StubHttpJsonService : IHttpJsonService
    {
        public ValueTask<TResponse> GetAsync<TResponse>(string requestUri, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public ValueTask<TResponse> PostAsync<TRequest, TResponse>(string requestUri, TRequest requestBody, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public ValueTask<TResponse> SendAsync<TResponse>(HttpRequestMessage request, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public ValueTask<HttpRawResponse> SendRawAsync(HttpRequestMessage request, HttpRequestExecutionOptions? options = null, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
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