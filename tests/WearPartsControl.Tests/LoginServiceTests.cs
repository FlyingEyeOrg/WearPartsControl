using System.Globalization;
using System.IO;
using System.Net.Http;
using WearPartsControl.ApplicationServices;
using WearPartsControl.ApplicationServices.HttpService;
using WearPartsControl.ApplicationServices.Localization;
using WearPartsControl.ApplicationServices.Localization.Generated;
using WearPartsControl.ApplicationServices.LoginService;
using Xunit;

namespace WearPartsControl.Tests;

public sealed class LoginServiceTests : IDisposable
{
    private readonly string _settingsDirectory;

    public LoginServiceTests()
    {
        _settingsDirectory = Path.Combine(Path.GetTempPath(), $"wearparts-login-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_settingsDirectory);
    }

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

        var service = new LoginService(new StubHttpJsonService(), new StubLocalizationService(), currentUserAccessor, new MhrUserDirectoryCache(Path.Combine(_settingsDirectory, "cache.json")), CreateConfigPath());

        await service.LogoutAsync();

        Assert.Null(service.GetCurrentUser());
    }

    [Fact]
    public async Task LoginAsync_ShouldUseCachedUsersBeforeCallingRemote()
    {
        var currentUserAccessor = new CurrentUserAccessor();
        var cache = new MhrUserDirectoryCache(Path.Combine(_settingsDirectory, "cache.json"));
        await cache.SaveUsersAsync(
            "S01",
            "RES-01",
            [new MhrUser { CardId = "CARD-01", WorkId = "WORK-01", AccessLevel = 2 }],
            DateTime.UtcNow);

        var httpJsonService = new RecordingHttpJsonService();
        var service = new LoginService(httpJsonService, new StubLocalizationService(), currentUserAccessor, cache, CreateConfigPath());

        var user = await service.LoginAsync("CARD-01", "S01", "RES-01", isIdCard: true);

        Assert.NotNull(user);
        Assert.Equal("WORK-01", user!.WorkId);
        Assert.Equal(0, httpJsonService.PostCallCount);
        Assert.Equal(0, httpJsonService.SendCallCount);
        Assert.Equal("WORK-01", service.GetCurrentUser()!.WorkId);
    }

    [Fact]
    public async Task LoginAsync_ShouldPersistFetchedUsersIntoCache()
    {
        var currentUserAccessor = new CurrentUserAccessor();
        var cachePath = Path.Combine(_settingsDirectory, "cache.json");
        var cache = new MhrUserDirectoryCache(cachePath);
        var httpJsonService = new RecordingHttpJsonService
        {
            Token = "token-01",
            Response = new MhrUserListResponse
            {
                Success = true,
                Data = new MhrUserListData
                {
                    Users =
                    [
                        new MhrUser { CardId = "CARD-02", WorkId = "WORK-02", AccessLevel = 3 }
                    ]
                }
            }
        };

        var service = new LoginService(httpJsonService, new StubLocalizationService(), currentUserAccessor, cache, CreateConfigPath());

        var user = await service.LoginAsync("CARD-02", "S01", "RES-02", isIdCard: true);
        var cachedUser = await cache.FindUserAsync("S01", "RES-02", "CARD-02", isIdCard: true, cacheDays: 1);

        Assert.NotNull(user);
        Assert.NotNull(cachedUser);
        Assert.Equal(1, httpJsonService.PostCallCount);
        Assert.Equal(1, httpJsonService.SendCallCount);
        Assert.True(File.Exists(cachePath));
    }

    public void Dispose()
    {
        if (Directory.Exists(_settingsDirectory))
        {
            Directory.Delete(_settingsDirectory, recursive: true);
        }
    }

    private string CreateConfigPath()
    {
        var path = Path.Combine(_settingsDirectory, "mhrinfo.json");
        File.WriteAllText(path, """
{
  "LoginName": "tester",
  "Password": "secret",
  "CacheDays": 1,
  "LoginInfos": [
    {
      "site": "S01",
      "loginUrl": "https://example.com/token",
      "getUsersUrl": "https://example.com/users"
    }
  ]
}
""");
        return path;
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

    private sealed class RecordingHttpJsonService : IHttpJsonService
    {
        public int PostCallCount { get; private set; }

        public int SendCallCount { get; private set; }

        public string Token { get; set; } = string.Empty;

        public MhrUserListResponse Response { get; set; } = new();

        public ValueTask<TResponse> GetAsync<TResponse>(string requestUri, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public ValueTask<TResponse> PostAsync<TRequest, TResponse>(string requestUri, TRequest requestBody, CancellationToken cancellationToken = default)
        {
            PostCallCount++;
            return ValueTask.FromResult((TResponse)(object)Token);
        }

        public ValueTask<TResponse> SendAsync<TResponse>(HttpRequestMessage request, CancellationToken cancellationToken = default)
        {
            SendCallCount++;
            return ValueTask.FromResult((TResponse)(object)Response);
        }

        public ValueTask<HttpRawResponse> SendRawAsync(HttpRequestMessage request, HttpRequestExecutionOptions? options = null, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}