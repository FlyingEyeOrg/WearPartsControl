using System.Text.Json.Serialization;

namespace WearPartsControl.ApplicationServices.ComNotification;

internal sealed class ComPushData
{
    [JsonPropertyName("code")]
    public int Code { get; set; }

    [JsonPropertyName("errMessage")]
    public string? ErrMessage { get; set; }

    [JsonPropertyName("messageId")]
    public string? MessageId { get; set; }
}