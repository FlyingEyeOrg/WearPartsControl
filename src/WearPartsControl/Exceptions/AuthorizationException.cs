using System;

namespace WearPartsControl.Exceptions;

public class AuthorizationException : BusinessException
{
    public AuthorizationException(string message, string? code = null, Exception? innerException = null)
        : base(message, code, null, innerException)
    {
    }
}
