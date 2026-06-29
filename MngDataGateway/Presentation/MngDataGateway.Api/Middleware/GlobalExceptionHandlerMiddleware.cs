using System.Net;
using MngDataGateway.Application.DTOs.Common;
using MngDataGateway.Domain.Constants;
using MngDataGateway.Domain.Exceptions;
using MngDataGateway.Persistence.Helpers;

namespace MngDataGateway.Api.Middleware;

public class GlobalExceptionHandlerMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandlerMiddleware> _logger;

    public GlobalExceptionHandlerMiddleware(RequestDelegate next, ILogger<GlobalExceptionHandlerMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unhandled exception occurred: {Message}", ex.Message);
            await HandleExceptionAsync(context, ex);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var path = context.Request.Path.Value ?? "/";
        var dgEx = exception is DataGatewayException existing
            ? existing
            : DgExceptionMapper.Map(exception, "An unexpected error occurred");

        var (statusCode, errorCode, message, details) = MapToResponse(dgEx, path);

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = statusCode;

        var response = new ErrorResponseDto
        {
            Success = false,
            Error = new ErrorDetailDto
            {
                Code = errorCode,
                Message = message,
                Details = details
            },
            Meta = new ResponseMetaDto
            {
                Timestamp = DateTime.UtcNow,
                Path = path
            }
        };

        await context.Response.WriteAsJsonAsync(response);
    }

    private static (int StatusCode, string ErrorCode, string Message, object? Details) MapToResponse(
        DataGatewayException ex,
        string path)
    {
        return ex switch
        {
            ConflictException conflict => (
                StatusCodes.Status409Conflict,
                ErrorCodes.DUPLICATE_KEY,
                conflict.Message,
                conflict.ValidationErrors),

            DataGatewayException dg when dg is ValidationException || dg.ValidationErrors is { Count: > 0 } => (
                StatusCodes.Status400BadRequest,
                ErrorCodes.VALIDATION_ERROR,
                dg.Message,
                dg.ValidationErrors),

            NotFoundException => (
                StatusCodes.Status404NotFound,
                ex.ErrorCode ?? ErrorCodes.RESOURCE_NOT_FOUND,
                ex.Message,
                null),

            UnauthorizedException => (
                StatusCodes.Status401Unauthorized,
                ErrorCodes.UNAUTHORIZED,
                ex.Message,
                null),

            ForbiddenException => (
                StatusCodes.Status403Forbidden,
                ErrorCodes.FORBIDDEN,
                ex.Message,
                null),

            BadRequestException badRequest => (
                StatusCodes.Status400BadRequest,
                badRequest.ErrorCode ?? ErrorCodes.INVALID_ARGUMENT,
                badRequest.Message,
                badRequest.ValidationErrors ?? (object?)badRequest.InnerException?.Message),

            _ when DgExceptionMapper.IsClientError(ex) => (
                StatusCodes.Status400BadRequest,
                ex.ErrorCode ?? ErrorCodes.INVALID_ARGUMENT,
                ex.Message,
                ex.ValidationErrors ?? (object?)ex.InnerException?.Message),

            _ => (
                StatusCodes.Status500InternalServerError,
                ex.ErrorCode ?? ErrorCodes.INTERNAL_ERROR,
                ex.Message,
                ex.InnerException?.Message)
        };
    }
}

public static class GlobalExceptionHandlerMiddlewareExtensions
{
    public static IApplicationBuilder UseGlobalExceptionHandler(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<GlobalExceptionHandlerMiddleware>();
    }
}
