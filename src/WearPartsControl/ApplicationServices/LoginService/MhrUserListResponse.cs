namespace WearPartsControl.ApplicationServices.LoginService;

public sealed class MhrUserListResponse
{
    public bool Success { get; set; }

    public MhrUserListData Data { get; set; } = new();
}
