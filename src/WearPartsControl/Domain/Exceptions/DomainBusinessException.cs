using System;
using WearPartsControl.Exceptions;

namespace WearPartsControl.Domain.Exceptions;

public class DomainBusinessException : BusinessException
{
    public DomainBusinessException(
        string message,
        string? code = null,
        string? details = null,
        Exception? innerException = null)
        : base(message, code, details, innerException)
    {
    }
}
