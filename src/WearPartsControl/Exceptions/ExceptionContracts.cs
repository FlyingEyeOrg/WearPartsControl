using System.Collections.Generic;

namespace WearPartsControl.Exceptions;

public interface IHasErrorCode
{
    string? Code { get; }
}

public interface IHasErrorDetails
{
    string? Details { get; }
}

public interface IHasValidationErrors
{
    IReadOnlyList<ValidationErrorInfo> ValidationErrors { get; }
}

public sealed record ValidationErrorInfo(string Message, string? Member = null);
