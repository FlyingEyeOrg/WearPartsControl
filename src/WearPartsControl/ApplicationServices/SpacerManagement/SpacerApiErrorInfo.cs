using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace WearPartsControl.ApplicationServices.SpacerManagement;

internal sealed class SpacerApiErrorInfo
{
    [JsonPropertyName("code")]
    public string? Code { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("details")]
    public string? Details { get; set; }

    [JsonPropertyName("data")]
    public Dictionary<string, object>? Data { get; set; }

    [JsonPropertyName("validationErrors")]
    public object? ValidationErrors { get; set; }
}