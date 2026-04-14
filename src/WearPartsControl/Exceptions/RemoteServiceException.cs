using System;

namespace WearPartsControl.Exceptions;

public class RemoteServiceException : BusinessException
{
    public RemoteServiceException(string message, string? code = null, Exception? innerException = null)
        : base(message, code, null, innerException)
    {
    }
}
