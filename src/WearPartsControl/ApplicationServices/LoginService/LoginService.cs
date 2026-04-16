using System.Text.Json;
using System.Net.Http;
using System.IO;
using System.Globalization;
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
    private readonly string _configPath = Path.Combine(PortableDataPaths.SettingsDirectory, "mhrinfo.json");

    public LoginService(IHttpJsonService httpJsonService, ILocalizationService localizationService, ICurrentUserAccessor currentUserAccessor)
    {
        _httpJsonService = httpJsonService;
        _localizationService = localizationService;
        _currentUserAccessor = currentUserAccessor;
    }

    public async Task<MhrUser?> LoginAsync(string authId, string factory, string resourceId, bool isIdCard, CancellationToken cancellationToken = default)
    {
        var config = await LoadConfigAsync(cancellationToken);
        var loginInfo = config.LoginInfos.FirstOrDefault(x => x.Site.Equals(factory, StringComparison.OrdinalIgnoreCase));
        if (loginInfo == null)
        {
            throw new UserFriendlyException(LF("LoginService.FactoryConfigNotFound", factory));
        }

        if (string.IsNullOrEmpty(config.Password))
        {
            throw new UserFriendlyException(LF("LoginService.PasswordEmpty", factory));
        }

        if (string.IsNullOrEmpty(config.LoginName))
        {
            throw new UserFriendlyException(LF("LoginService.LoginNameEmpty", factory));
        }

        if (string.IsNullOrEmpty(loginInfo.GetUsersUrl))
        {
            throw new UserFriendlyException(LF("LoginService.GetUsersUrlEmpty", factory));
        }

        if (string.IsNullOrEmpty(loginInfo.LoginUrl))
        {
            throw new UserFriendlyException(LF("LoginService.LoginUrlEmpty", factory));
        }

        // Get token
        var token = await GetTokenAsync(loginInfo.LoginUrl, config.LoginName, config.Password, cancellationToken);
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        // Get user list
        var result = await GetUserListAsync(loginInfo.GetUsersUrl, token, factory, resourceId, cancellationToken);
        if (result?.Success != true || result.Data.Users == null || result.Data.Users.Count == 0)
        {
            throw new UserFriendlyException(L("LoginService.UserListEmpty"));
        }

        // Find user
        var user = result.Data.Users.FirstOrDefault(x => isIdCard ? x.CardId == authId : x.WorkId == authId);
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
        var response = await _httpJsonService.PostAsync<object, string>(loginUrl, request, cancellationToken);
        return response; // Assume response is token string
    }

    private async Task<MhrUserListResponse> GetUserListAsync(string getUsersUrl, string token, string factory, string resourceId, CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"{getUsersUrl}?factory={factory}&resourceId={resourceId}");
        request.Headers.Add("Authorization", $"Bearer {token}");
        var response = await _httpJsonService.SendAsync<MhrUserListResponse>(request, cancellationToken);
        return response;
    }

    private string L(string key) => _localizationService[key];

    private string LF(string key, params object[] args) => string.Format(CultureInfo.CurrentCulture, L(key), args);
}