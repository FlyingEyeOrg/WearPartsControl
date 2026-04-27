using System.Text.Json.Serialization;

namespace WearPartsControl.ApplicationServices.ComNotification;

internal sealed class ComPushResponse
{
    [JsonPropertyName("code")]
    public int Code { get; set; }

    [JsonPropertyName("msg")]
    public string? Msg { get; set; }

    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("data")]
    public ComPushData? Data { get; set; }
}