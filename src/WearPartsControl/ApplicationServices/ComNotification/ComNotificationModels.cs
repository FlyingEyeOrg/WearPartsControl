using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace WearPartsControl.ApplicationServices.ComNotification;

internal sealed class ComPushRequest
{
    [JsonPropertyName("agent_id")]
    public long AgentId { get; set; }

    [JsonPropertyName("template_data")]
    public ComPushTemplateData TemplateData { get; set; } = new();

    [JsonPropertyName("isAt")]
    public bool? IsAt { get; set; }

    [JsonPropertyName("template_id")]
    public long TemplateId { get; set; }

    [JsonPropertyName("to_all")]
    public bool ToAll { get; set; }

    [JsonPropertyName("to_user")]
    public List<string> ToUser { get; set; } = new();

    [JsonPropertyName("user_type")]
    public string UserType { get; set; } = string.Empty;
}

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

internal sealed class ComPushData
{
    [JsonPropertyName("code")]
    public int Code { get; set; }

    [JsonPropertyName("errMessage")]
    public string? ErrMessage { get; set; }

    [JsonPropertyName("messageId")]
    public string? MessageId { get; set; }
}
