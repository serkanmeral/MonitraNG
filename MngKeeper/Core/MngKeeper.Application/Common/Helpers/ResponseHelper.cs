using MngKeeper.Application.Common.Exceptions;
using Microsoft.Extensions.Logging;

namespace MngKeeper.Application.Common.Helpers;

/// <summary>
/// Helper class for creating consistent response objects
/// </summary>
public static class ResponseHelper
{
    /// <summary>
    /// Creates an error response with consistent error handling
    /// </summary>
    public static TResponse CreateErrorResponse<TResponse>(
        ILogger logger,
        Exception exception,
        string operationName,
        params object[] contextArgs) where TResponse : class, new()
    {
        ExceptionHelper.LogException(logger, exception, operationName, contextArgs);
        
        var response = new TResponse();
        
        // Use reflection to set IsSuccess and ErrorMessage properties if they exist
        var responseType = typeof(TResponse);
        var isSuccessProperty = responseType.GetProperty("IsSuccess");
        var errorMessageProperty = responseType.GetProperty("ErrorMessage");
        
        if (isSuccessProperty != null && isSuccessProperty.CanWrite)
        {
            isSuccessProperty.SetValue(response, false);
        }
        
        if (errorMessageProperty != null && errorMessageProperty.CanWrite)
        {
            var errorMessage = ExceptionHelper.GetUserFriendlyMessage(exception);
            errorMessageProperty.SetValue(response, errorMessage);
        }
        
        return response;
    }
    
    /// <summary>
    /// Creates a simple error response with a custom message
    /// </summary>
    public static TResponse CreateErrorResponse<TResponse>(
        string errorMessage) where TResponse : class, new()
    {
        var response = new TResponse();
        
        var responseType = typeof(TResponse);
        var isSuccessProperty = responseType.GetProperty("IsSuccess");
        var errorMessageProperty = responseType.GetProperty("ErrorMessage");
        
        if (isSuccessProperty != null && isSuccessProperty.CanWrite)
        {
            isSuccessProperty.SetValue(response, false);
        }
        
        if (errorMessageProperty != null && errorMessageProperty.CanWrite)
        {
            errorMessageProperty.SetValue(response, errorMessage);
        }
        
        return response;
    }
}

