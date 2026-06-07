using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using MngNotifier.Application.Exceptions;
using MngNotifier.Application.Models;
using MngNotifier.Application.Services;
using MngNotifier.Application.Utilities;

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
        var hasBodyOverride = !string.IsNullOrWhiteSpace(request.BodyHtmlOverride);

        if (template == null && !hasBodyOverride)
            throw new TemplateRenderException($"Template not found: {request.TemplateKey}", 404);

        if (template?.IsActive == false && !hasBodyOverride)
            throw new TemplateRenderException($"Template is not active: {request.TemplateKey}");

        var subjectSource = !string.IsNullOrWhiteSpace(request.SubjectOverride)
            ? request.SubjectOverride
            : template?.Subject ?? string.Empty;

        var bodySource = hasBodyOverride
            ? request.BodyHtmlOverride!
            : template?.BodyHtml ?? string.Empty;

        if (string.IsNullOrWhiteSpace(subjectSource) || string.IsNullOrWhiteSpace(bodySource))
            throw new TemplateRenderException("Template subject and bodyHtml are required");

        var variables = template?.Variables;
        if (variables == null || variables.Count == 0)
            variables = ExtractPlaceholderPaths(subjectSource, bodySource);

        ValidateRequiredVariables(variables, request.Context);

        var layoutKey = !string.IsNullOrWhiteSpace(request.LayoutKeyOverride)
            ? request.LayoutKeyOverride.Trim()
            : string.IsNullOrWhiteSpace(template?.LayoutKey) ? "default" : template!.LayoutKey!.Trim();

        var layout = await _dg.GetLayoutByKeyAsync(layoutKey, bearerToken, cancellationToken)
            ?? await _dg.GetDefaultLayoutAsync(bearerToken, cancellationToken);

        if (layout == null || layout.IsActive == false)
            throw new TemplateRenderException($"Layout not found or inactive: {layoutKey}", 404);

        var locale = !string.IsNullOrWhiteSpace(request.LocaleOverride)
            ? request.LocaleOverride.Trim()
            : string.IsNullOrWhiteSpace(template?.Locale) ? null : template!.Locale!.Trim();

        var renderedSubject = ReplacePlaceholders(subjectSource, request.Context, htmlEncode: false, locale);
        var renderedBodyFragment = ReplacePlaceholders(bodySource, request.Context, htmlEncode: true, locale);
        renderedBodyFragment = ScriptTagRegex.Replace(renderedBodyFragment, string.Empty);

        var renderedHeader = ReplacePlaceholders(layout.HeaderHtml ?? string.Empty, request.Context, htmlEncode: true, locale);
        var renderedFooter = ReplacePlaceholders(layout.FooterHtml ?? string.Empty, request.Context, htmlEncode: true, locale);
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

    private static List<string> ExtractPlaceholderPaths(params string?[] sources)
    {
        var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var source in sources)
        {
            if (string.IsNullOrWhiteSpace(source))
                continue;

            foreach (Match match in PlaceholderRegex.Matches(source))
            {
                var (path, _) = PlaceholderFormatting.ParsePlaceholderExpression(match.Groups[1].Value.Trim());
                if (!string.IsNullOrWhiteSpace(path))
                    found.Add(path);
            }
        }

        return found.OrderBy(x => x, StringComparer.Ordinal).ToList();
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

            var (normalizedPath, _) = PlaceholderFormatting.ParsePlaceholderExpression(path.Trim());
            var value = ResolvePath(context, normalizedPath);
            if (string.IsNullOrWhiteSpace(value))
                missing.Add(normalizedPath);
        }

        if (missing.Count > 0)
            throw new TemplateRenderException($"Missing required context variables: {string.Join(", ", missing)}");
    }

    private static string ReplacePlaceholders(string input, JsonElement context, bool htmlEncode, string? locale)
    {
        return PlaceholderRegex.Replace(input, match =>
        {
            var (path, formatHint) = PlaceholderFormatting.ParsePlaceholderExpression(match.Groups[1].Value.Trim());
            var raw = ResolvePath(context, path) ?? string.Empty;
            var value = PlaceholderFormatting.FormatValue(raw, path, formatHint, locale);
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
