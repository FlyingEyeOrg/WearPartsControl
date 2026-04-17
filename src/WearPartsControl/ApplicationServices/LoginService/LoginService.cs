using System.Text.Json;
using System.Net.Http;
using System.IO;
using System.Globalization;
using System.Net.Http.Headers;
using WearPartsControl.ApplicationServices;
using WearPartsControl.ApplicationServices.HttpService;
using WearPartsControl.ApplicationServices.Localization;
using WearPartsControl.Exceptions;

namespace WearPartsControl.ApplicationServices.LoginService;

public sealed class LoginService : ILoginService
{
    private readonly IHttpJsonService _httpJsonService;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly ILocalizationService _localizationService;
    private readonly IMhrUserDirectoryCache _mhrUserDirectoryCache;
    private readonly string _configPath;

    public LoginService(
        IHttpJsonService httpJsonService,
        ILocalizationService localizationService,
        ICurrentUserAccessor currentUserAccessor,
        IMhrUserDirectoryCache mhrUserDirectoryCache,
        string? configPath = null)
    {
        _httpJsonService = httpJsonService;
        _localizationService = localizationService;
        _currentUserAccessor = currentUserAccessor;
        _mhrUserDirectoryCache = mhrUserDirectoryCache;
        _configPath = Path.GetFullPath(configPath ?? Path.Combine(PortableDataPaths.SettingsDirectory, "mhrinfo.json"));
    }

    public async Task<MhrUser?> LoginAsync(string authId, string factory, string resourceId, bool isIdCard, CancellationToken cancellationToken = default)
    {
        var config = await LoadConfigAsync(cancellationToken);
        var normalizedFactory = factory?.Trim() ?? string.Empty;
        var normalizedResourceId = resourceId?.Trim() ?? string.Empty;
        var normalizedAuthId = authId?.Trim() ?? string.Empty;
        var loginInfo = config.LoginInfos.FirstOrDefault(x => x.Site.Equals(normalizedFactory, StringComparison.OrdinalIgnoreCase));
        if (loginInfo == null)
        {
            throw new UserFriendlyException(LF("LoginService.FactoryConfigNotFound", normalizedFactory));
        }

        if (string.IsNullOrEmpty(config.Password))
        {
            throw new UserFriendlyException(LF("LoginService.PasswordEmpty", normalizedFactory));
        }

        if (string.IsNullOrEmpty(config.LoginName))
        {
            throw new UserFriendlyException(LF("LoginService.LoginNameEmpty", normalizedFactory));
        }

        if (string.IsNullOrEmpty(loginInfo.GetUsersUrl))
        {
            throw new UserFriendlyException(LF("LoginService.GetUsersUrlEmpty", normalizedFactory));
        }

        if (string.IsNullOrEmpty(loginInfo.LoginUrl))
        {
            throw new UserFriendlyException(LF("LoginService.LoginUrlEmpty", normalizedFactory));
        }

        // Get token
        var cachedUser = await _mhrUserDirectoryCache.FindUserAsync(normalizedFactory, normalizedResourceId, normalizedAuthId, isIdCard, config.CacheDays, cancellationToken).ConfigureAwait(false);
        if (cachedUser is not null)
        {
            _currentUserAccessor.SetCurrentUser(cachedUser);
            return cachedUser;
        }

        var token = await GetTokenAsync(loginInfo.LoginUrl, config.LoginName, config.Password, cancellationToken);
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        // Get user list
        var result = await GetUserListAsync(loginInfo.GetUsersUrl, token, normalizedFactory, normalizedResourceId, cancellationToken);
        if (result?.Success != true || result.Data.Users == null || result.Data.Users.Count == 0)
        {
            throw new UserFriendlyException(L("LoginService.UserListEmpty"));
        }

        await _mhrUserDirectoryCache.SaveUsersAsync(normalizedFactory, normalizedResourceId, result.Data.Users, DateTime.UtcNow, cancellationToken).ConfigureAwait(false);

        // Find user
        var user = result.Data.Users.FirstOrDefault(x => isIdCard ? x.CardId == normalizedAuthId : x.WorkId == normalizedAuthId);
        if (user is null)
        {
            _currentUserAccessor.Clear();
            return null;
        }

        _currentUserAccessor.SetCurrentUser(user);
        return user;
    }

    public MhrUser? GetCurrentUser()
    {
        return _currentUserAccessor.CurrentUser;
    }

    public ValueTask LogoutAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _currentUserAccessor.Clear();
        return ValueTask.CompletedTask;
    }

    private async Task<MhrConfig> LoadConfigAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_configPath))
        {
            throw new UserFriendlyException(LF("LoginService.ConfigFileNotFound", _configPath));
        }

        var json = await File.ReadAllTextAsync(_configPath, cancellationToken);
        var config = JsonSerializer.Deserialize<MhrConfig>(json);
        if (config == null)
        {
            throw new UserFriendlyException(L("LoginService.ConfigDeserializeFailed"));
        }

        return config;
    }

    private async Task<string> GetTokenAsync(string loginUrl, string loginName, string password, CancellationToken cancellationToken)
    {
        var request = new { loginName, password };
        var response = await _httpJsonService.PostAsync<object, JsonElement>(loginUrl, request, cancellationToken).ConfigureAwait(false);
        return ParseAuthorizationValue(response);
    }

    private async Task<MhrUserListResponse> GetUserListAsync(string getUsersUrl, string token, string factory, string resourceId, CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"{getUsersUrl}?factory={factory}&resourceId={resourceId}");
        request.Headers.TryAddWithoutValidation("Authorization", NormalizeAuthorizationHeader(token));
        var response = await _httpJsonService.SendAsync<MhrUserListResponse>(request, cancellationToken);
        return response;
    }

    private static string ParseAuthorizationValue(JsonElement response)
    {
        if (response.ValueKind == JsonValueKind.String)
        {
            return response.GetString()?.Trim() ?? string.Empty;
        }

        if (response.ValueKind == JsonValueKind.Object)
        {
            if (TryGetStringProperty(response, "Authorization", out var authorization)
                || TryGetStringProperty(response, "AccessToken", out authorization)
                || TryGetStringProperty(response, "Token", out authorization))
            {
                return authorization;
            }
        }

        throw new UserFriendlyException("登录接口返回的 token 格式不正确。", code: "LoginService:InvalidTokenPayload");
    }

    private static bool TryGetStringProperty(JsonElement element, string propertyName, out string value)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase)
                && property.Value.ValueKind == JsonValueKind.String)
            {
                value = property.Value.GetString()?.Trim() ?? string.Empty;
                return !string.IsNullOrWhiteSpace(value);
            }
        }

        value = string.Empty;
        return false;
    }

    private static string NormalizeAuthorizationHeader(string token)
    {
        var normalized = token?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.Empty;
        }

        if (AuthenticationHeaderValue.TryParse(normalized, out var headerValue)
            && !string.IsNullOrWhiteSpace(headerValue.Scheme)
            && !string.IsNullOrWhiteSpace(headerValue.Parameter))
        {
            return normalized;
        }

        return $"Bearer {normalized}";
    }

    private string L(string key) => _localizationService[key];

    private string LF(string key, params object[] args) => string.Format(CultureInfo.CurrentCulture, L(key), args);
}