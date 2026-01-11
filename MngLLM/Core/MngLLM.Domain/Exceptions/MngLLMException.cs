namespace MngLLM.Domain.Exceptions;

/// <summary>
/// Base exception for MngLLM domain
/// </summary>
public class MngLLMException : Exception
{
    public MngLLMException(string message) : base(message) { }
    
    public MngLLMException(string message, Exception innerException) 
        : base(message, innerException) { }
}

/// <summary>
/// LLM service-related exceptions
/// </summary>
public class LLMServiceException : MngLLMException
{
    public LLMServiceException(string message) : base(message) { }
    
    public LLMServiceException(string message, Exception innerException) 
        : base(message, innerException) { }
}

/// <summary>
/// Translation-related exceptions
/// </summary>
public class TranslationException : MngLLMException
{
    public TranslationException(string message) : base(message) { }
    
    public TranslationException(string message, Exception innerException) 
        : base(message, innerException) { }
}

/// <summary>
/// Validation exceptions
/// </summary>
public class ValidationException : MngLLMException
{
    public List<string> ValidationErrors { get; set; } = new();

    public ValidationException(string message) : base(message) { }
    
    public ValidationException(string message, List<string> validationErrors) 
        : base(message)
    {
        ValidationErrors = validationErrors;
    }
}
