namespace WearPartsControl.ApplicationServices.LoginService;

public sealed class MhrUserListResponse
{
    public bool Success { get; set; }

    public int ErrorCode { get; set; }

    public string Msg { get; set; } = string.Empty;

    public MhrUserListData Data { get; set; } = new();
}
