using System;

namespace WearPartsControl.Exceptions;

public class UserFriendlyException : BusinessException
{
    public UserFriendlyException(
        string message,
        string? code = null,
        string? details = null,
        Exception? innerException = null)
        : base(message, code, details, innerException)
    {
    }
}
