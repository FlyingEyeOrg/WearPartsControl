using System;

namespace WearPartsControl.Exceptions;

public class EntityNotFoundException : BusinessException
{
    public EntityNotFoundException(string message, string? code = null, Exception? innerException = null)
        : base(message, code, null, innerException)
    {
    }
}
