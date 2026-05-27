using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using MngOperations.Application.Exceptions;

namespace MngOperations.Api.Filters;

public sealed class OperationCoreExceptionFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        if (context.Exception is not OperationCoreException ex)
            return;

        context.Result = new ObjectResult(new
        {
            code = ex.Code,
            message = ex.Message,
            messageTr = ex.MessageTr,
            details = ex.Details
        })
        {
            StatusCode = ex.StatusCode
        };
        context.ExceptionHandled = true;
    }
}
