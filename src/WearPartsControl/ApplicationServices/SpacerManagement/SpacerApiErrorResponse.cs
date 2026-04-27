using System.Text.Json.Serialization;

namespace WearPartsControl.ApplicationServices.SpacerManagement;

internal sealed class SpacerApiErrorResponse
{
    [JsonPropertyName("error")]
    public SpacerApiErrorInfo? Error { get; set; }
}