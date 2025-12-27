using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MngDataGateway.Application.DTOs.Common;
using MngDataGateway.Domain.Exceptions;
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
                Code = "VALIDATION_ERROR",
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
        ILogger? logger = null)
    {
        logger?.LogWarning(ex, "Resource not found at {Path}: {Message}", path, ex.Message);

        return controller.NotFound(new ErrorResponseDto
        {
            Success = false,
            Error = new ErrorDetailDto
            {
                Code = "DATASET_NOT_FOUND",
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
    /// Handle generic exception with logging
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
                    : ex.Message
            },
            Meta = CreateMeta(path)
        };

        return controller.StatusCode(500, errorResponse);
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
}

