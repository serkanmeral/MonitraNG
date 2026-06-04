using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using MngReactor.Application.Abstractions.Crypt;

namespace MngReactor.Api.Middleware;

/// <summary>
/// X-Payload-Format: encrypted ise Ingest POST body'yi çözüp JSON ile değiştirir.
/// Geriye uyumluluk: header yoksa mevcut body aynen kalır.
/// </summary>
public class IngestDecryptMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<IngestDecryptMiddleware> _logger;

    public IngestDecryptMiddleware(RequestDelegate next, ILogger<IngestDecryptMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ICryptProcessing cryptProcessing)
    {
        if (!IsIngestDecryptPost(context))
        {
            await _next(context);
            return;
        }

        var format = context.Request.Headers["X-Payload-Format"].FirstOrDefault();
        if (!string.Equals(format, "encrypted", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        try
        {
            using var reader = new StreamReader(context.Request.Body, Encoding.UTF8);
            var body = await reader.ReadToEndAsync(context.RequestAborted);
            if (string.IsNullOrWhiteSpace(body))
            {
                context.Response.StatusCode = 400;
                await context.Response.WriteAsJsonAsync(new { error = "empty_body", message = "Encrypted payload is empty" });
                return;
            }

            var encrypted = Convert.FromBase64String(body.Trim());
            var decryptedJson = await cryptProcessing.DeCompress(encrypted);

            var jsonBytes = Encoding.UTF8.GetBytes(decryptedJson);
            context.Request.Body = new MemoryStream(jsonBytes);
            context.Request.ContentType = "application/json";
            context.Request.ContentLength = jsonBytes.Length;

            await _next(context);
        }
        catch (FormatException ex)
        {
            _logger.LogWarning(ex, "Ingest decrypt: Base64 decode hatası");
            context.Response.StatusCode = 400;
            await context.Response.WriteAsJsonAsync(new { error = "invalid_base64", message = "Payload is not valid Base64" });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ingest decrypt hatası");
            context.Response.StatusCode = 400;
            await context.Response.WriteAsJsonAsync(new { error = "decrypt_failed", message = ex.Message });
        }
    }

    private static bool IsIngestDecryptPost(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "";
        if (context.Request.Method != HttpMethods.Post
            || !path.Contains("ingest", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return path.EndsWith("/metrics", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith("/sec-events", StringComparison.OrdinalIgnoreCase);
    }
}
