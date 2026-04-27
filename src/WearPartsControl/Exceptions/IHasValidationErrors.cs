namespace WearPartsControl.Exceptions;

public interface IHasValidationErrors
{
    IReadOnlyList<ValidationErrorInfo> ValidationErrors { get; }
}