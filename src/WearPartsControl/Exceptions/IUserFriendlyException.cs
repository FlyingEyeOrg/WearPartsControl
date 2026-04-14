using System;

namespace WearPartsControl.Exceptions;

/// <summary>
/// 兼容 ABP 风格的友好异常接口，便于框架集成与判断。
/// </summary>
public interface IUserFriendlyException
{
    /// <summary>
    /// 本地化/显示友好的消息
    /// </summary>
    string Message { get; }

    /// <summary>
    /// 可选的错误码
    /// </summary>
    string? Code { get; }

    /// <summary>
    /// 可选的详细信息
    /// </summary>
    string? Details { get; }
}
