using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using MngNotifier.Application.Exceptions;
using MngNotifier.Application.Models;
using MngNotifier.Application.Services;
using MngNotifier.Application.Utilities;

namespace MngNotifier.Infrastructure.Services;

public sealed class MessageTemplateRenderService : IMessageTemplateRenderService
{
    private static readonly Regex PlaceholderRegex = new(@"\{\{([^{}]+)\}\}", RegexOptions.Compiled);

    private readonly IDataGatewayTemplateClient _dg;
    private readonly ILogger<MessageTemplateRenderService> _logger;

    public MessageTemplateRenderService(
        IDataGatewayTemplateClient dg,
        ILogger<MessageTemplateRenderService> logger)
    {
        _dg = dg;
        _logger = logger;
    }

    public async Task<RenderedMessageContent> RenderAsync(
        MessageTemplateRenderRequest request,
        string bearerToken,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.TemplateKey))
            throw new TemplateRenderException("TemplateKey is required");

        var template = await _dg.GetMessageTemplateByKeyAsync(request.TemplateKey.Trim(), bearerToken, cancellationToken);
        var hasBodyOverride = !string.IsNullOrWhiteSpace(request.BodyTextOverride);

        if (template == null && !hasBodyOverride)
            throw new TemplateRenderException($"Message template not found: {request.TemplateKey}", 404);

        if (template?.IsActive == false && !hasBodyOverride)
            throw new TemplateRenderException($"Message template is not active: {request.TemplateKey}");

        var bodySource = hasBodyOverride
            ? request.BodyTextOverride!
            : template?.BodyText ?? string.Empty;

        if (string.IsNullOrWhiteSpace(bodySource))
            throw new TemplateRenderException("Template bodyText is required");

        var variables = template?.Variables;
        if (variables == null || variables.Count == 0)
            variables = ExtractPlaceholderPaths(bodySource);

        ValidateRequiredVariables(variables, request.Context);

        var locale = !string.IsNullOrWhiteSpace(request.LocaleOverride)
            ? request.LocaleOverride.Trim()
            : string.IsNullOrWhiteSpace(template?.Locale) ? null : template!.Locale!.Trim();

        var rendered = ReplacePlaceholders(bodySource, request.Context, locale);

        var parseMode = !string.IsNullOrWhiteSpace(request.ParseModeOverride)
            ? request.ParseModeOverride.Trim()
            : string.IsNullOrWhiteSpace(template?.ParseMode) ? null : template!.ParseMode!.Trim();

        _logger.LogDebug("Rendered message template {TemplateKey}", request.TemplateKey);

        return new RenderedMessageContent
        {
            Text = rendered,
            TemplateKey = request.TemplateKey.Trim(),
            ParseMode = parseMode,
            Channel = string.IsNullOrWhiteSpace(template?.Channel) ? null : template!.Channel!.Trim()
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

    private static string ReplacePlaceholders(string input, JsonElement context, string? locale)
    {
        return PlaceholderRegex.Replace(input, match =>
        {
            var (path, formatHint) = PlaceholderFormatting.ParsePlaceholderExpression(match.Groups[1].Value.Trim());
            var raw = ResolvePath(context, path) ?? string.Empty;
            return PlaceholderFormatting.FormatValue(raw, path, formatHint, locale);
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
}
