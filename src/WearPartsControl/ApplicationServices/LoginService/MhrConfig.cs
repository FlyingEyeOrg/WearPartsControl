using System.Text.Json.Serialization;

namespace WearPartsControl.ApplicationServices.LoginService;

public class MhrConfig
{
    [JsonPropertyName("LoginName")]
    public string LoginName { get; set; } = string.Empty;

    [JsonPropertyName("Password")]
    public string Password { get; set; } = string.Empty;

    [JsonPropertyName("LoginInfos")]
    public List<MhrSiteLoginInfo> LoginInfos { get; set; } = new();
}

public class MhrSiteLoginInfo
{
    [JsonPropertyName("site")]
    public string Site { get; set; } = string.Empty;

    [JsonPropertyName("loginUrl")]
    public string LoginUrl { get; set; } = string.Empty;

    [JsonPropertyName("getUsersUrl")]
    public string GetUsersUrl { get; set; } = string.Empty;
}