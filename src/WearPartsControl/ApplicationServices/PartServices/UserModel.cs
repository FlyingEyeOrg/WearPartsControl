namespace WearPartsControl.ApplicationServices.PartServices;

/// <summary>
/// MHR 人员信息。
/// </summary>
public sealed class UserModel
{
    /// <summary>
    /// 工号。
    /// </summary>
    public string work_id { get; set; } = string.Empty;

    /// <summary>
    /// 权限等级。
    /// </summary>
    public int access_level { get; set; }

    /// <summary>
    /// 卡号。
    /// </summary>
    public string card_id { get; set; } = string.Empty;
}
