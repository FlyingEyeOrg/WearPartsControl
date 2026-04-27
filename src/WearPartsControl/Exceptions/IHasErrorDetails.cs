namespace WearPartsControl.Exceptions;

public interface IHasErrorDetails
{
    string? Details { get; }
}