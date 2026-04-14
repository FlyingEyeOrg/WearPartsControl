using System.Text.Json;
using System.Net.Http;
using System.IO;
using WearPartsControl.ApplicationServices.HttpService;
using WearPartsControl.Exceptions;

namespace WearPartsControl.ApplicationServices.LoginService;

public sealed class LoginService : ILoginService
{
    private readonly IHttpJsonService _httpJsonService;
    private readonly string _configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings", "mhrinfo.json");

    public LoginService(IHttpJsonService httpJsonService)
    {
        _httpJsonService = httpJsonService;
    }

    public async Task<UserModel?> LoginAsync(string authId, string factory, string resourceId, bool isIdCard, CancellationToken cancellationToken = default)
    {
        var config = await LoadConfigAsync(cancellationToken);
        var loginInfo = config.LoginInfos.FirstOrDefault(x => x.Site.Equals(factory, StringComparison.OrdinalIgnoreCase));
        if (loginInfo == null)
        {
            throw new UserFriendlyException($"配置文件中不存在 {factory} 基地的 MHR 配置项");
        }

        if (string.IsNullOrEmpty(config.Password))
        {
            throw new UserFriendlyException($"{factory} 基地的 MHR 认证密码为空");
        }

        if (string.IsNullOrEmpty(config.LoginName))
        {
            throw new UserFriendlyException($"{factory} 基地的 MHR 登录名为空");
        }

        if (string.IsNullOrEmpty(loginInfo.GetUsersUrl))
        {
            throw new UserFriendlyException($"{factory} 基地的 MHR 用户认证 URL 为空");
        }

        if (string.IsNullOrEmpty(loginInfo.LoginUrl))
        {
            throw new UserFriendlyException($"{factory} 基地的 MHR 用户登录 URL 为空");
        }

        // Get token
        var token = await GetTokenAsync(loginInfo.LoginUrl, config.LoginName, config.Password, cancellationToken);
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        // Get user list
        var result = await GetUserListAsync(loginInfo.GetUsersUrl, token, factory, resourceId, cancellationToken);
        if (result?.Success != true || result.Data.list == null || result.Data.list.Count == 0)
        {
            throw new Exception("未获取到当前资源号的用户列表信息");
        }

        // Find user
        var user = result.Data.list.FirstOrDefault(x => isIdCard ? x.card_id == authId : x.work_id == authId);
        return user;
    }

    private async Task<MhrConfig> LoadConfigAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_configPath))
        {
            throw new FileNotFoundException("mhrinfo.json not found", _configPath);
        }

        var json = await File.ReadAllTextAsync(_configPath, cancellationToken);
        var config = JsonSerializer.Deserialize<MhrConfig>(json);
        if (config == null)
        {
            throw new InvalidOperationException("Failed to deserialize mhrinfo.json");
        }
        return config;
    }

    private async Task<string> GetTokenAsync(string loginUrl, string loginName, string password, CancellationToken cancellationToken)
    {
        var request = new { loginName, password };
        var response = await _httpJsonService.PostAsync<object, string>(loginUrl, request, cancellationToken);
        return response; // Assume response is token string
    }

    private async Task<HMRResult> GetUserListAsync(string getUsersUrl, string token, string factory, string resourceId, CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"{getUsersUrl}?factory={factory}&resourceId={resourceId}");
        request.Headers.Add("Authorization", $"Bearer {token}");
        var response = await _httpJsonService.SendAsync<HMRResult>(request, cancellationToken);
        return response;
    }
}