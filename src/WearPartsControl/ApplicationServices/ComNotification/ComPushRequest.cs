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