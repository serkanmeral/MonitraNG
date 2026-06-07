using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using MngNotifier.Application.Exceptions;
using MngNotifier.Application.Models;
using MngNotifier.Application.Services;

namespace MngNotifier.Infrastructure.Services;

public sealed class TemplateRenderService : ITemplateRenderService
{
    private static readonly Regex PlaceholderRegex = new(@"\{\{([^{}]+)\}\}", RegexOptions.Compiled);
    private static readonly Regex ScriptTagRegex = new(@"<script\b[^<]*(?:(?!<\/script>)<[^<]*)*<\/script>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex EmptyImgRegex = new(@"<img\b[^>]*\bsrc\s*=\s*[""']\s*[""'][^>]*\/?>", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly IDataGatewayTemplateClient _dg;
    private readonly ILogger<TemplateRenderService> _logger;

    public TemplateRenderService(IDataGatewayTemplateClient dg, ILogger<TemplateRenderService> logger)
    {
        _dg = dg;
        _logger = logger;
    }

    public async Task<RenderedMailContent> RenderAsync(
        TemplateRenderRequest request,
        string bearerToken,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.TemplateKey))
            throw new TemplateRenderException("TemplateKey is required");

        var template = await _dg.GetTemplateByKeyAsync(request.TemplateKey.Trim(), bearerToken, cancellationToken);
        if (template == null)
            throw new TemplateRenderException($"Template not found: {request.TemplateKey}", 404);

        if (template.IsActive == false)
            throw new TemplateRenderException($"Template is not active: {request.TemplateKey}");

        ValidateRequiredVariables(template.Variables, request.Context);

        var layoutKey = string.IsNullOrWhiteSpace(template.LayoutKey) ? "default" : template.LayoutKey.Trim();
        var layout = await _dg.GetLayoutByKeyAsync(layoutKey, bearerToken, cancellationToken)
            ?? await _dg.GetDefaultLayoutAsync(bearerToken, cancellationToken);

        if (layout == null || layout.IsActive == false)
            throw new TemplateRenderException($"Layout not found or inactive: {layoutKey}", 404);

        var subjectSource = !string.IsNullOrWhiteSpace(request.SubjectOverride)
            ? request.SubjectOverride
            : template.Subject ?? string.Empty;

        var renderedSubject = ReplacePlaceholders(subjectSource, request.Context, htmlEncode: false);
        var renderedBodyFragment = ReplacePlaceholders(template.BodyHtml ?? string.Empty, request.Context, htmlEncode: true);
        renderedBodyFragment = ScriptTagRegex.Replace(renderedBodyFragment, string.Empty);

        var renderedHeader = ReplacePlaceholders(layout.HeaderHtml ?? string.Empty, request.Context, htmlEncode: true);
        var renderedFooter = ReplacePlaceholders(layout.FooterHtml ?? string.Empty, request.Context, htmlEncode: true);
        renderedHeader = StripEmptyLogoImages(renderedHeader, request.Context);
        renderedHeader = EmptyImgRegex.Replace(renderedHeader, string.Empty);

        var styles = WebUtility.HtmlEncode(layout.StylesCss ?? string.Empty);
        var fullHtml = $"""
            <!DOCTYPE html>
            <html>
            <head><meta charset="utf-8"><style>{styles}</style></head>
            <body>
            <div class="email-wrapper">
            <div class="email-container">
            {renderedHeader}
            <div class="email-body">{renderedBodyFragment}</div>
            {renderedFooter}
            </div>
            </div>
            </body>
            </html>
            """;

        _logger.LogDebug("Rendered template {TemplateKey} with layout {LayoutKey}", request.TemplateKey, layout.LayoutKey);

        return new RenderedMailContent
        {
            Subject = renderedSubject,
            HtmlBody = fullHtml,
            TemplateKey = request.TemplateKey,
            LayoutKey = layout.LayoutKey
        };
    }

    private static void ValidateRequiredVariables(IReadOnlyList<string>? variables, JsonElement context)
    {
        if (variables == null || variables.Count == 0)
            return;

        var missing = new List<string>();
        foreach (var path in variables)
        {
            if (string.IsNullOrWhiteSpace(path))
                continue;

            var value = ResolvePath(context, path.Trim());
            if (string.IsNullOrWhiteSpace(value))
                missing.Add(path.Trim());
        }

        if (missing.Count > 0)
            throw new TemplateRenderException($"Missing required context variables: {string.Join(", ", missing)}");
    }

    private static string ReplacePlaceholders(string input, JsonElement context, bool htmlEncode)
    {
        return PlaceholderRegex.Replace(input, match =>
        {
            var path = match.Groups[1].Value.Trim();
            var value = ResolvePath(context, path) ?? string.Empty;
            return htmlEncode ? WebUtility.HtmlEncode(value) : value;
        });
    }

    private static string? ResolvePath(JsonElement context, string path)
    {
        if (context.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
            return null;

        var segments = path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var current = context;

        foreach (var segment in segments)
        {
            if (current.ValueKind != JsonValueKind.Object)
                return null;

            if (!current.TryGetProperty(segment, out current))
                return null;
        }

        return current.ValueKind switch
        {
            JsonValueKind.String => current.GetString(),
            JsonValueKind.Number => current.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => null,
            _ => current.GetRawText()
        };
    }

    private static string StripEmptyLogoImages(string html, JsonElement context)
    {
        var logoUrl = ResolvePath(context, "domain.logoUrl");
        if (!string.IsNullOrWhiteSpace(logoUrl))
            return html;

        return Regex.Replace(html, @"<img\b[^>]*\{\{domain\.logoUrl\}\}[^>]*\/?>", string.Empty, RegexOptions.IgnoreCase);
    }
}
