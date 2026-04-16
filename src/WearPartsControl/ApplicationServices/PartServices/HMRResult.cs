namespace WearPartsControl.ApplicationServices.PartServices;

/// <summary>
/// MHR 返回结果。
/// </summary>
public sealed class MhrResult
{
    /// <summary>
    /// 请求是否成功。
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// 错误码。
    /// </summary>
    public int ErrorCode { get; set; }

    /// <summary>
    /// 错误消息。
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// 返回的数据体。
    /// </summary>
    public MhrData Data { get; set; } = new();
}
