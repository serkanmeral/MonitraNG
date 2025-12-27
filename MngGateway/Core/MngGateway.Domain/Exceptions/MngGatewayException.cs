namespace MngGateway.Domain.Exceptions;

public class MngGatewayException : Exception
{
    public MngGatewayException(string message) : base(message)
    {
    }

    public MngGatewayException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

