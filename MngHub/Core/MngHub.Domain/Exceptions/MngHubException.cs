namespace MngHub.Domain.Exceptions;

/// <summary>
/// Base exception for MngHub domain
/// </summary>
public class MngHubException : Exception
{
    public MngHubException(string message) : base(message) { }
    
    public MngHubException(string message, Exception innerException) 
        : base(message, innerException) { }
}

/// <summary>
/// Connection-related exceptions
/// </summary>
public class ConnectionException : MngHubException
{
    public ConnectionException(string message) : base(message) { }
    
    public ConnectionException(string message, Exception innerException) 
        : base(message, innerException) { }
}

/// <summary>
/// Validation exceptions
/// </summary>
public class ValidationException : MngHubException
{
    public List<string> ValidationErrors { get; set; } = new();

    public ValidationException(string message) : base(message) { }
    
    public ValidationException(string message, List<string> validationErrors) 
        : base(message)
    {
        ValidationErrors = validationErrors;
    }
}

/// <summary>
/// JWT validation exceptions
/// </summary>
public class JwtValidationException : MngHubException
{
    public JwtValidationException(string message) : base(message) { }
    
    public JwtValidationException(string message, Exception innerException) 
        : base(message, innerException) { }
}

/// <summary>
/// RabbitMQ-related exceptions
/// </summary>
public class RabbitMqException : MngHubException
{
    public RabbitMqException(string message) : base(message) { }
    
    public RabbitMqException(string message, Exception innerException) 
        : base(message, innerException) { }
}

