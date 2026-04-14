using System;
using System.Collections.Generic;
using System.Linq;

namespace WearPartsControl.Exceptions;

public class ValidationException : BusinessException, IHasValidationErrors
{
    public ValidationException(
        string message,
        IEnumerable<ValidationErrorInfo>? validationErrors = null,
        string? code = null,
        string? details = null,
        Exception? innerException = null)
        : base(message, code, details, innerException)
    {
        ValidationErrors = validationErrors?.ToList() ?? new List<ValidationErrorInfo>();
    }

    public IReadOnlyList<ValidationErrorInfo> ValidationErrors { get; }
}
