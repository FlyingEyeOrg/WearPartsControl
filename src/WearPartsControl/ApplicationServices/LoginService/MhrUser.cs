using System.Text.Json.Serialization;

namespace WearPartsControl.ApplicationServices.LoginService;

public sealed class MhrUser
{
    [JsonPropertyName("card_id")]
    public string CardId { get; set; } = string.Empty;

    [JsonPropertyName("work_id")]
    public string WorkId { get; set; } = string.Empty;
}
