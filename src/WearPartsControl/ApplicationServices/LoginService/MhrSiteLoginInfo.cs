using System.Text.Json.Serialization;

namespace WearPartsControl.ApplicationServices.LoginService;

public class MhrSiteLoginInfo
{
    [JsonPropertyName("site")]
    public string Site { get; set; } = string.Empty;

    [JsonPropertyName("loginUrl")]
    public string LoginUrl { get; set; } = string.Empty;

    [JsonPropertyName("getUsersUrl")]
    public string GetUsersUrl { get; set; } = string.Empty;
}