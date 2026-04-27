using System.Text.Json.Serialization;

namespace WearPartsControl.ApplicationServices.ComNotification;

internal sealed class ComPushTemplateData
{
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("access_token")]
    public string? AccessToken { get; set; }

    [JsonPropertyName("secret")]
    public string? Secret { get; set; }
}