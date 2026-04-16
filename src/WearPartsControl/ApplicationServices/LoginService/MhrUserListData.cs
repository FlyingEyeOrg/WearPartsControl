using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace WearPartsControl.ApplicationServices.LoginService;

public sealed class MhrUserListData
{
    [JsonPropertyName("timestamp")]
    public long Timestamp { get; set; }

    [JsonPropertyName("list")]
    public List<MhrUser> Users { get; set; } = new();
}
