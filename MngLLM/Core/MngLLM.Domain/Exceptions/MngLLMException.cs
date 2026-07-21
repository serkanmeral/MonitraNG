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

/// <summary>Document / extract related exceptions.</summary>
public class DiExtractException : MngLLMException
{
    public int StatusCode { get; }

    public DiExtractException(string message, int statusCode = 400) : base(message)
    {
        StatusCode = statusCode;
    }

    public DiExtractException(string message, int statusCode, Exception innerException)
        : base(message, innerException)
    {
        StatusCode = statusCode;
    }
}

