using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace WearPartsControl.ApplicationServices.SpacerManagement;

public sealed class SpacerInfo
{
    public string Site { get; set; } = string.Empty;

    public string ResourceId { get; set; } = string.Empty;

    public string Operator { get; set; } = string.Empty;

    public string ModelPn { get; set; } = string.Empty;

    public string Date { get; set; } = string.Empty;

    public string Thickness { get; set; } = string.Empty;

    public string BigCoatingWidth { get; set; } = string.Empty;

    public string SmallCoatingWidth { get; set; } = string.Empty;

    public string WhiteSpaceWidth { get; set; } = string.Empty;

    public string AT11Width { get; set; } = string.Empty;

    public string ABSite { get; set; } = string.Empty;
}

internal sealed class SpacerApiErrorResponse
{
    [JsonPropertyName("error")]
    public SpacerApiErrorInfo? Error { get; set; }
}

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
