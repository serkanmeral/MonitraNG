using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using MngDocument.Application.Exceptions;

namespace MngDocument.Api.Filters;

public sealed class DocumentExceptionFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        if (context.Exception is not DocumentException ex)
            return;

        context.Result = new ObjectResult(new
        {
            code = ex.Code,
            message = ex.Message,
            messageTr = ex.MessageTr
        })
        {
            StatusCode = ex.StatusCode
        };
        context.ExceptionHandled = true;
    }
}
