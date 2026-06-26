using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using MngDocument.Application.Exceptions;

namespace MngDocument.Api.Filters;

public sealed class DocumentExceptionFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        if (context.Exception is DocumentException ex)
        {
            context.Result = ErrorResult(ex.StatusCode, ex.Code, ex.Message, ex.MessageTr);
            context.ExceptionHandled = true;
            return;
        }

        if (context.Exception is HttpRequestException httpEx)
        {
            var status = httpEx.StatusCode ?? HttpStatusCode.InternalServerError;
            var (code, messageTr) = MapDataGatewayError(httpEx.Message);
            var statusCode = status == HttpStatusCode.BadRequest ? 400 : (int)status;
            context.Result = ErrorResult(statusCode, code, httpEx.Message, messageTr);
            context.ExceptionHandled = true;
        }
    }

    private static ObjectResult ErrorResult(int statusCode, string code, string message, string? messageTr) =>
        new(new { code, message, messageTr })
        {
            StatusCode = statusCode
        };

    private static (string Code, string? MessageTr) MapDataGatewayError(string message)
    {
        if (message.Contains("VALIDATION_ERROR", StringComparison.OrdinalIgnoreCase)
            || message.Contains("Validation failed", StringComparison.OrdinalIgnoreCase))
        {
            return ("VALIDATION_ERROR", "Veri doğrulama hatası. Dataset şeması veya alan değerlerini kontrol edin.");
        }

        return ("DATA_GATEWAY_ERROR", "Veri kaydı sırasında hata oluştu.");
    }
}
