using System;
using System.Collections.Generic;
using WearPartsControl.Exceptions;

namespace WearPartsControl.Domain.Exceptions;

public sealed class DomainValidationException : ValidationException
{
    public DomainValidationException(
        string message,
        IEnumerable<ValidationErrorInfo>? validationErrors = null,
        string? code = null,
        string? details = null,
        Exception? innerException = null)
        : base(message, validationErrors, code, details, innerException)
    {
    }
}
