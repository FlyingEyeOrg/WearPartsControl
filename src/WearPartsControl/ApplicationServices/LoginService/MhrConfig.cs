using System.Text.Json.Serialization;

namespace WearPartsControl.ApplicationServices.LoginService;

public class MhrConfig
{
    [JsonPropertyName("LoginName")]
    public string LoginName { get; set; } = string.Empty;

    [JsonPropertyName("Password")]
    public string Password { get; set; } = string.Empty;

    [JsonPropertyName("CacheDays")]
    public int CacheDays { get; set; } = 1;

    [JsonPropertyName("LoginInfos")]
    public List<MhrSiteLoginInfo> LoginInfos { get; set; } = new();
}