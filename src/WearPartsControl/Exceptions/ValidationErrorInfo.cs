namespace WearPartsControl.Exceptions;

public sealed record ValidationErrorInfo(string Message, string? Member = null);