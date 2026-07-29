using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;
using MngLogCollector.Application.Configuration;

namespace MngLogCollector.Api.Filters;

/// <summary>Simple shared-secret gate for field agents (MVP). Replace with enrollment tokens later.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class IngestApiKeyAttribute : Attribute, IAsyncActionFilter
{
    public const string HeaderName = "X-MngLogs-ApiKey";

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var settings = context.HttpContext.RequestServices
            .GetRequiredService<IOptions<MngLogCollectorSettings>>().Value;

        var expected = settings.Ingest.ApiKey ?? string.Empty;
        if (string.IsNullOrWhiteSpace(expected))
        {
            await next();
            return;
        }

        if (!context.HttpContext.Request.Headers.TryGetValue(HeaderName, out var provided) ||
            !FixedTimeEquals(provided.ToString(), expected))
        {
            context.Result = new ContentResult
            {
                StatusCode = (int)HttpStatusCode.Unauthorized,
                Content = "Invalid or missing ingest API key."
            };
            return;
        }

        await next();
    }

    private static bool FixedTimeEquals(string a, string b)
    {
        if (a.Length != b.Length)
            return false;

        var diff = 0;
        for (var i = 0; i < a.Length; i++)
            diff |= a[i] ^ b[i];
        return diff == 0;
    }
}
