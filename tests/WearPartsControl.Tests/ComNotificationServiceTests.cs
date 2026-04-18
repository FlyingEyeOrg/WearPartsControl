using System.IO;
using System.Net.Http;
using System.Text.Json;
using WearPartsControl.ApplicationServices.ComNotification;
using WearPartsControl.ApplicationServices.HttpService;
using WearPartsControl.ApplicationServices.Localization;
using WearPartsControl.ApplicationServices.SaveInfoService;
using WearPartsControl.ApplicationServices.UserConfig;
using Xunit;

namespace WearPartsControl.Tests;

public sealed class ComNotificationServiceTests
{
    [Fact]
    public async Task NotifyGroupAsync_ShouldUseUserConfigCredentialsAndRecipientsWhenUsersNotProvided()
    {
        var settingsDirectory = Path.Combine(Path.GetTempPath(), $"WearPartsControl.ComNotify.{Guid.NewGuid():N}");
        Directory.CreateDirectory(settingsDirectory);

        try
        {
            var store = new TypeJsonSaveInfoStore(settingsDirectory);
            await store.WriteAsync(new ComNotificationOptionsSaveInfo
            {
                Enabled = true,
                PushUrl = "https://example.com",
                DeIpaasKeyAuth = "auth-key",
                UserType = "ding",
                AccessToken = "legacy-token",
                Secret = "legacy-secret",
                DefaultUserWorkId = "LEGACY001"
            });

            var userConfigService = new UserConfigService(store);
            await userConfigService.SaveAsync(new UserConfig
            {
                MeResponsibleWorkId = "ME1001",
                PrdResponsibleWorkId = "PRD1001",
                ComAccessToken = "user-token",
                ComSecret = "user-secret"
            });

            var httpJsonService = new StubHttpJsonService();
            var service = new ComNotificationService(store, new StubLocalizationService(), httpJsonService, userConfigService);

            await service.NotifyGroupAsync("title", "text");

            Assert.NotNull(httpJsonService.LastRequestBody);
            Assert.Contains("ME1001", httpJsonService.LastRequestBody!);
            Assert.Contains("PRD1001", httpJsonService.LastRequestBody!);
            Assert.DoesNotContain("LEGACY001", httpJsonService.LastRequestBody!);
            Assert.Contains("user-token", httpJsonService.LastRequestBody!);
            Assert.Contains("user-secret", httpJsonService.LastRequestBody!);
        }
        finally
        {
            if (Directory.Exists(settingsDirectory))
            {
                Directory.Delete(settingsDirectory, true);
            }
        }
    }

    private sealed class StubHttpJsonService : IHttpJsonService
    {
        public string? LastRequestBody { get; private set; }

        public ValueTask<TResponse> GetAsync<TResponse>(string requestUri, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public ValueTask<TResponse> PostAsync<TRequest, TResponse>(string requestUri, TRequest requestBody, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public ValueTask<TResponse> SendAsync<TResponse>(HttpRequestMessage request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public async ValueTask<HttpRawResponse> SendRawAsync(HttpRequestMessage request, HttpRequestExecutionOptions? options = null, CancellationToken cancellationToken = default)
        {
            LastRequestBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpRawResponse(200, "OK", JsonSerializer.Serialize(new
            {
                code = 0,
                success = true,
                data = new
                {
                    code = 0,
                    errMessage = string.Empty,
                    messageId = "msg-001"
                }
            }));
        }
    }

    private sealed class StubLocalizationService : ILocalizationService
    {
        public string this[string name] => name;

        public ApplicationServices.Localization.Generated.LocalizationCatalog Catalog { get; } = new(static key => key);

        public ValueTask InitializeAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public ValueTask SetCultureAsync(string cultureName, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public System.Globalization.CultureInfo CurrentCulture { get; } = System.Globalization.CultureInfo.GetCultureInfo("zh-CN");
    }
}