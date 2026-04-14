using System;

namespace WearPartsControl.ApplicationServices.Exceptions;

public interface IFriendlyException
{
    string? Code { get; }

    string? Details { get; }
}

public class FriendlyException : Exception, IFriendlyException
{
    public FriendlyException(string message, string? code = null, string? details = null, Exception? innerException = null)
        : base(message, innerException)
    {
        Code = code;
        Details = details;
    }

    public string? Code { get; }

    public string? Details { get; }
}
