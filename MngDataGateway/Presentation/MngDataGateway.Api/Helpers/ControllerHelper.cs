using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MngDataGateway.Application.DTOs.Common;
using MngDataGateway.Domain.Constants;
using MngDataGateway.Domain.Exceptions;
using MngDataGateway.Persistence.Helpers;
using System;

namespace MngDataGateway.Api.Helpers;

/// <summary>
/// Base helper class for controllers with common response building and error handling methods
/// </summary>
public static class ControllerHelper
{
    /// <summary>
    /// Create a success response with data
    /// </summary>
    public static IActionResult SuccessResponse<T>(this ControllerBase controller, T data, string path)
    {
        return controller.Ok(new DataResponseDto<T>
        {
            Success = true,
            Data = data,
            Meta = CreateMeta(path)
        });
    }

    /// <summary>
    /// Create a success response without data
    /// </summary>
    public static IActionResult SuccessResponse(this ControllerBase controller, string path, object? data = null)
    {
        return controller.Ok(new
        {
            Success = true,
            Data = data,
            Meta = CreateMeta(path)
        });
    }

    /// <summary>
    /// Unified handler for any exception — maps infrastructure errors then returns appropriate HTTP status.
    /// </summary>
    public static IActionResult HandleException(
        this ControllerBase controller,
        Exception ex,
        string path,
        string serverErrorCode,
        string serverErrorMessage,
        ILogger? logger = null,
        bool includeStackTrace = false)
    {
        var dgEx = ex is DataGatewayException existing
            ? existing
            : DgExceptionMapper.Map(ex, serverErrorMessage);

        return controller.HandleDataGatewayException(
            dgEx, path, serverErrorCode, serverErrorMessage, logger, includeStackTrace);
    }

    /// <summary>
    /// Maps typed DataGateway exceptions to HTTP responses.
    /// </summary>
    public static IActionResult HandleDataGatewayException(
        this ControllerBase controller,
        DataGatewayException ex,
        string path,
        string serverErrorCode,
        string serverErrorMessage,
        ILogger? logger = null,
        bool includeStackTrace = false)
    {
        switch (ex)
        {
            case ConflictException conflict:
                logger?.LogWarning(conflict, "Conflict at {Path}", path);
                return controller.ErrorResponse(
                    path,
                    ErrorCodes.DUPLICATE_KEY,
                    conflict.Message,
                    conflict.ValidationErrors,
                    statusCode: StatusCodes.Status409Conflict);

            case ValidationException:
            case DataGatewayException dg when dg.ValidationErrors is { Count: > 0 }:
                return controller.HandleValidationError(ex, path, logger);

            case NotFoundException notFound:
                return controller.HandleNotFoundError(notFound, path, logger, ResolveNotFoundCode(notFound));

            case UnauthorizedException unauthorized:
                logger?.LogWarning(unauthorized, "Unauthorized at {Path}", path);
                return controller.ErrorResponse(
                    path,
                    ErrorCodes.UNAUTHORIZED,
                    unauthorized.Message,
                    statusCode: StatusCodes.Status401Unauthorized);

            case ForbiddenException forbidden:
                logger?.LogWarning(forbidden, "Forbidden at {Path}", path);
                return controller.ErrorResponse(
                    path,
                    ErrorCodes.FORBIDDEN,
                    forbidden.Message,
                    statusCode: StatusCodes.Status403Forbidden);

            case BadRequestException badRequest:
                logger?.LogWarning(badRequest, "Bad request at {Path}", path);
                return controller.ErrorResponse(
                    path,
                    badRequest.ErrorCode ?? ErrorCodes.INVALID_ARGUMENT,
                    badRequest.Message,
                    badRequest.ValidationErrors ?? (object?)badRequest.InnerException?.Message,
                    statusCode: StatusCodes.Status400BadRequest);

            default:
                if (DgExceptionMapper.IsClientError(ex))
                {
                    logger?.LogWarning(ex, "Client error at {Path}: {Message}", path, ex.Message);
                    return controller.ErrorResponse(
                        path,
                        ex.ErrorCode ?? ErrorCodes.INVALID_ARGUMENT,
                        ex.Message,
                        ex.ValidationErrors ?? (object?)ex.InnerException?.Message,
                        statusCode: StatusCodes.Status400BadRequest);
                }

                return controller.HandleError(
                    ex,
                    path,
                    ex.ErrorCode ?? serverErrorCode,
                    serverErrorMessage,
                    logger,
                    includeStackTrace);
        }
    }

    /// <summary>
    /// Handle DataGatewayException with validation errors
    /// </summary>
    public static IActionResult HandleValidationError(
        this ControllerBase controller,
        DataGatewayException ex,
        string path,
        ILogger? logger = null)
    {
        logger?.LogWarning(ex, "Validation error at {Path}", path);

        return controller.BadRequest(new ErrorResponseDto
        {
            Success = false,
            Error = new ErrorDetailDto
            {
                Code = ErrorCodes.VALIDATION_ERROR,
                Message = ex.Message,
                Details = ex.ValidationErrors
            },
            Meta = CreateMeta(path)
        });
    }

    /// <summary>
    /// Handle DataGatewayException with "not found" message
    /// </summary>
    public static IActionResult HandleNotFoundError(
        this ControllerBase controller,
        DataGatewayException ex,
        string path,
        ILogger? logger = null,
        string? errorCode = null)
    {
        logger?.LogWarning(ex, "Resource not found at {Path}: {Message}", path, ex.Message);

        return controller.NotFound(new ErrorResponseDto
        {
            Success = false,
            Error = new ErrorDetailDto
            {
                Code = errorCode ?? ex.ErrorCode ?? ErrorCodes.RESOURCE_NOT_FOUND,
                Message = ex.Message
            },
            Meta = CreateMeta(path)
        });
    }

    /// <summary>
    /// Handle generic DataGatewayException
    /// </summary>
    public static IActionResult HandleDataGatewayError(
        this ControllerBase controller,
        DataGatewayException ex,
        string path,
        string errorCode,
        ILogger? logger = null)
    {
        logger?.LogWarning(ex, "DataGateway error at {Path}: {Message}", path, ex.Message);

        return controller.BadRequest(new ErrorResponseDto
        {
            Success = false,
            Error = new ErrorDetailDto
            {
                Code = errorCode,
                Message = ex.Message,
                Details = ex.InnerException?.Message
            },
            Meta = CreateMeta(path)
        });
    }

    /// <summary>
    /// Handle generic exception with logging (true server errors)
    /// </summary>
    public static IActionResult HandleError(
        this ControllerBase controller,
        Exception ex,
        string path,
        string errorCode,
        string errorMessage,
        ILogger? logger = null,
        bool includeStackTrace = false)
    {
        logger?.LogError(ex, "Error at {Path}: {Message}", path, ex.Message);

        var errorResponse = new ErrorResponseDto
        {
            Success = false,
            Error = new ErrorDetailDto
            {
                Code = errorCode,
                Message = errorMessage,
                Details = includeStackTrace
                    ? new
                    {
                        message = ex.Message,
                        innerException = ex.InnerException?.Message,
                        stackTrace = ex.StackTrace?.Split('\n').Take(5).ToArray()
                    }
                    : ex.InnerException?.Message ?? ex.Message
            },
            Meta = CreateMeta(path)
        };

        return controller.StatusCode(StatusCodes.Status500InternalServerError, errorResponse);
    }

    /// <summary>
    /// Create response metadata
    /// </summary>
    public static ResponseMetaDto CreateMeta(string path)
    {
        return new ResponseMetaDto
        {
            Timestamp = DateTime.UtcNow,
            Path = path
        };
    }

    /// <summary>
    /// Create error response with custom details
    /// </summary>
    public static IActionResult ErrorResponse(
        this ControllerBase controller,
        string path,
        string errorCode,
        string errorMessage,
        object? details = null,
        int statusCode = 400)
    {
        var errorResponse = new ErrorResponseDto
        {
            Success = false,
            Error = new ErrorDetailDto
            {
                Code = errorCode,
                Message = errorMessage,
                Details = details
            },
            Meta = CreateMeta(path)
        };

        return controller.StatusCode(statusCode, errorResponse);
    }

    private static string ResolveNotFoundCode(NotFoundException ex)
    {
        if (!string.IsNullOrEmpty(ex.ErrorCode))
            return ex.ErrorCode;

        if (ex.Message.Contains("Dataset", StringComparison.OrdinalIgnoreCase))
            return ErrorCodes.DATASET_NOT_FOUND;

        if (ex.Message.Contains("query", StringComparison.OrdinalIgnoreCase))
            return ErrorCodes.QUERY_NOT_FOUND;

        if (ex.Message.Contains("__dataId", StringComparison.OrdinalIgnoreCase))
            return ErrorCodes.DATA_NOT_FOUND;

        return ErrorCodes.RESOURCE_NOT_FOUND;
    }
}
