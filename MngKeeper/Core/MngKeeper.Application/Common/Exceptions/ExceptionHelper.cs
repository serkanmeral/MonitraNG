using MongoDB.Driver;
using System.Net;
using Microsoft.Extensions.Logging;

namespace MngKeeper.Application.Common.Exceptions
{
    /// <summary>
    /// Helper class for consistent exception handling across the application
    /// </summary>
    public static class ExceptionHelper
    {
        /// <summary>
        /// Determines if an exception is a transient error that might succeed on retry
        /// </summary>
        public static bool IsTransientException(Exception exception)
        {
            return exception switch
            {
                MongoConnectionException => true,
                TimeoutException => true,
                HttpRequestException httpEx when httpEx.InnerException is TimeoutException => true,
                _ => false
            };
        }

        /// <summary>
        /// Determines if an exception is a client error (4xx) that shouldn't be retried
        /// </summary>
        public static bool IsClientError(Exception exception)
        {
            if (exception is HttpRequestException httpEx)
            {
                // Check if it's a 4xx status code
                return httpEx.Data.Contains("StatusCode") && 
                       httpEx.Data["StatusCode"] is HttpStatusCode statusCode &&
                       (int)statusCode >= 400 && (int)statusCode < 500;
            }
            return false;
        }

        /// <summary>
        /// Determines if an exception is a server error (5xx) that might succeed on retry
        /// </summary>
        public static bool IsServerError(Exception exception)
        {
            if (exception is HttpRequestException httpEx)
            {
                // Check if it's a 5xx status code
                return httpEx.Data.Contains("StatusCode") && 
                       httpEx.Data["StatusCode"] is HttpStatusCode statusCode &&
                       (int)statusCode >= 500 && (int)statusCode < 600;
            }
            return false;
        }

        /// <summary>
        /// Gets a user-friendly error message from an exception
        /// </summary>
        public static string GetUserFriendlyMessage(Exception exception)
        {
            return exception switch
            {
                MongoConnectionException => "Database connection error. Please try again later.",
                MongoWriteException mongoWrite when mongoWrite.WriteError?.Category == ServerErrorCategory.DuplicateKey => 
                    "A record with this value already exists.",
                MongoWriteException => "Database write error. Please check your input and try again.",
                TimeoutException => "The operation timed out. Please try again.",
                HttpRequestException httpEx when IsClientError(httpEx) => 
                    "Invalid request. Please check your input.",
                HttpRequestException httpEx when IsServerError(httpEx) => 
                    "Server error. Please try again later.",
                HttpRequestException => "Network error. Please check your connection and try again.",
                UnauthorizedAccessException => "You don't have permission to perform this action.",
                ArgumentException argEx => $"Invalid input: {argEx.Message}",
                InvalidOperationException invOpEx => $"Operation failed: {invOpEx.Message}",
                _ => "An unexpected error occurred. Please try again or contact support."
            };
        }

        /// <summary>
        /// Logs an exception with appropriate log level based on exception type
        /// </summary>
        public static void LogException(ILogger logger, Exception exception, string context, params object[] args)
        {
            var logLevel = GetLogLevel(exception);
            var message = $"Error in {context}";
            
            // Use the appropriate log method based on log level
            switch (logLevel)
            {
                case LogLevel.Warning:
                    logger.LogWarning(exception, message, args);
                    break;
                case LogLevel.Error:
                    logger.LogError(exception, message, args);
                    break;
                case LogLevel.Critical:
                    logger.LogCritical(exception, message, args);
                    break;
                default:
                    logger.LogError(exception, message, args);
                    break;
            }
        }

        /// <summary>
        /// Gets the appropriate log level for an exception
        /// </summary>
        public static LogLevel GetLogLevel(Exception exception)
        {
            return exception switch
            {
                ArgumentException => LogLevel.Warning,
                UnauthorizedAccessException => LogLevel.Warning,
                InvalidOperationException => LogLevel.Warning,
                MongoConnectionException => LogLevel.Error,
                MongoWriteException => LogLevel.Error,
                TimeoutException => LogLevel.Warning,
                HttpRequestException httpEx when IsClientError(httpEx) => LogLevel.Warning,
                HttpRequestException httpEx when IsServerError(httpEx) => LogLevel.Error,
                _ => LogLevel.Error
            };
        }

        /// <summary>
        /// Determines if an exception should be rethrown or handled gracefully
        /// </summary>
        public static bool ShouldRethrow(Exception exception)
        {
            // Don't rethrow client errors or validation errors
            if (IsClientError(exception) || exception is ArgumentException || exception is InvalidOperationException)
            {
                return false;
            }
            
            // Rethrow server errors and connection errors (they might be retried at a higher level)
            return IsServerError(exception) || IsTransientException(exception);
        }
    }
}

