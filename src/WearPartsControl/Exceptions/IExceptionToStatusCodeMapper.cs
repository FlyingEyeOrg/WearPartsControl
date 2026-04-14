using System.Net;

namespace WearPartsControl.Exceptions;

public interface IExceptionToStatusCodeMapper
{
    HttpStatusCode Map(Exception exception);
}
