using System;

namespace WearPartsControl.Exceptions;

public class UserFriendlyException : BusinessException, IUserFriendlyException
{
    public UserFriendlyException(
        string message,
        string? code = null,
        string? details = null,
        Exception? innerException = null)
        : base(message, code, details, innerException)
    {
    }

    // Message/Code/Details are provided by base class properties and interfaces
    string IUserFriendlyException.Message => Message!;
    string? IUserFriendlyException.Code => Code;
    string? IUserFriendlyException.Details => Details;
}
