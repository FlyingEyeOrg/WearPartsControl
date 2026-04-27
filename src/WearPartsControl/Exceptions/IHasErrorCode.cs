namespace WearPartsControl.Exceptions;

public interface IHasErrorCode
{
    string? Code { get; }
}