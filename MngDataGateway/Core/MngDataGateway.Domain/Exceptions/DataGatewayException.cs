using System.Collections.Generic;

namespace MngDataGateway.Domain.Exceptions;

public class DataGatewayException : Exception
{
    public string? ErrorCode { get; set; }
    public List<object>? ValidationErrors { get; set; }

    public DataGatewayException(string message) : base(message)
    {
    }

    public DataGatewayException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

public class ValidationException : DataGatewayException
{
    public ValidationException(string message) : base(message)
    {
        ErrorCode = Constants.ErrorCodes.VALIDATION_ERROR;
    }

    public static ValidationException FromErrors(string message, IEnumerable<object> errors) =>
        new(message) { ValidationErrors = errors is List<object> list ? list : errors.ToList() };
}

public class ConflictException : DataGatewayException
{
    public ConflictException(string message) : base(message)
    {
        ErrorCode = Constants.ErrorCodes.DUPLICATE_KEY;
    }
}

public class BadRequestException : DataGatewayException
{
    public BadRequestException(string message, string? errorCode = null) : base(message)
    {
        ErrorCode = errorCode ?? Constants.ErrorCodes.INVALID_ARGUMENT;
    }

    public BadRequestException(string message, Exception innerException, string? errorCode = null)
        : base(message, innerException)
    {
        ErrorCode = errorCode ?? Constants.ErrorCodes.INVALID_ARGUMENT;
    }
}

public class NotFoundException : DataGatewayException
{
    public NotFoundException(string message) : base(message)
    {
        ErrorCode = Constants.ErrorCodes.RESOURCE_NOT_FOUND;
    }
}

public class UnauthorizedException : DataGatewayException
{
    public UnauthorizedException(string message) : base(message)
    {
        ErrorCode = Constants.ErrorCodes.UNAUTHORIZED;
    }
}

public class ForbiddenException : DataGatewayException
{
    public ForbiddenException(string message) : base(message)
    {
        ErrorCode = Constants.ErrorCodes.FORBIDDEN;
    }
}
