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
            return;
        }

        if (context.Exception is InvalidOperationException invalidOpEx)
        {
            var (code, message, messageTr, statusCode) = MapInvalidOperationError(invalidOpEx.Message);
            context.Result = ErrorResult(statusCode, code, message, messageTr);
            context.ExceptionHandled = true;
            return;
        }

        // Yakalanmamış hatalar — istemciye anlamlı Türkçe özet (ayrıntı log'da kalır).
        var unhandled = context.Exception;
        context.Result = ErrorResult(
            StatusCodes.Status500InternalServerError,
            "INTERNAL_ERROR",
            unhandled.Message,
            "Belge işlemi tamamlanamadı. Sunucu günlüklerini kontrol edin veya destek ekibine bildirin.");
        context.ExceptionHandled = true;
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

    private static (string Code, string Message, string MessageTr, int StatusCode) MapInvalidOperationError(
        string message)
    {
        if (message.Contains("Could not allocate counter", StringComparison.OrdinalIgnoreCase))
        {
            return (
                "COUNTER_ALLOCATION_FAILED",
                message,
                "Dosya numarası sayacı ayrılamadı. dm_generation_counters dataset'inin tanımlı olduğundan ve DataGateway erişilebilir olduğundan emin olun.",
                StatusCodes.Status503ServiceUnavailable);
        }

        if (message.Contains("Counter row id missing", StringComparison.OrdinalIgnoreCase))
        {
            return (
                "COUNTER_DATA_INVALID",
                message,
                "Sayaç kaydı geçersiz. dm_generation_counters verisini kontrol edin.",
                StatusCodes.Status503ServiceUnavailable);
        }

        if (message.Contains("Invalid cover document", StringComparison.OrdinalIgnoreCase)
            || message.Contains("Invalid body document", StringComparison.OrdinalIgnoreCase))
        {
            return (
                "COVER_PAGE_INVALID",
                message,
                "Kapak sayfası dosyası geçersiz veya bozuk. Kapak şablonunu yeniden kaydedin.",
                StatusCodes.Status400BadRequest);
        }

        return (
            "OPERATION_FAILED",
            message,
            "Belge işlemi tamamlanamadı. Girdiğiniz parametreleri ve şablon ayarlarını kontrol edin.",
            StatusCodes.Status500InternalServerError);
    }
}
