using System.Collections.Generic;

namespace MngDataGateway.Domain.Exceptions;

public class DataGatewayException : Exception
{
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
    }
}

public class NotFoundException : DataGatewayException
{
    public NotFoundException(string message) : base(message)
    {
    }
}

public class UnauthorizedException : DataGatewayException
{
    public UnauthorizedException(string message) : base(message)
    {
    }
}

