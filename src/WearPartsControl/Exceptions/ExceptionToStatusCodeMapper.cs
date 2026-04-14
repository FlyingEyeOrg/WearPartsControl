using System;
using System.Net;

namespace WearPartsControl.Exceptions;

public class ExceptionToStatusCodeMapper : IExceptionToStatusCodeMapper
{
    public HttpStatusCode Map(Exception exception)
    {
        if (exception is null) return HttpStatusCode.InternalServerError;

        // Authorization: 401 or 403
        if (exception is AuthorizationException) return HttpStatusCode.Forbidden;

        // Not found
        if (exception is EntityNotFoundException) return HttpStatusCode.NotFound;

        // Validation / user errors
        if (exception is ValidationException) return HttpStatusCode.BadRequest;
        if (exception is UserFriendlyException) return HttpStatusCode.BadRequest;

        // Remote service errors
        if (exception is RemoteServiceException) return HttpStatusCode.BadGateway;

        // Business exceptions default to 400
        if (exception is BusinessException) return HttpStatusCode.BadRequest;

        return HttpStatusCode.InternalServerError;
    }
}
