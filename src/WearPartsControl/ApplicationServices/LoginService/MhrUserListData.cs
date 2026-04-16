using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace WearPartsControl.ApplicationServices.LoginService;

public sealed class MhrUserListData
{
    [JsonPropertyName("list")]
    public List<MhrUser> Users { get; set; } = new();
}
