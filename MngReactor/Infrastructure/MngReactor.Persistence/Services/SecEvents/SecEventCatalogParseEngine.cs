using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using MngReactor.Application.Abstractions.SecEvents;
using MngReactor.Application.Models.SecEvents;
using MngReactor.Application.Services.SecEvents;
using MngReactor.Persistence.Services.SecEvents.Parsers;

namespace MngReactor.Persistence.Services.SecEvents;

public sealed class SecEventCatalogParseEngine : ISecEventCatalogParseEngine
{
    private readonly ISecEventParseRuleCatalogService _catalog;
    private readonly ISecEventParseRuleCatalogCache _cache;
    private readonly ILogger<SecEventCatalogParseEngine> _logger;

    public SecEventCatalogParseEngine(
        ISecEventParseRuleCatalogService catalog,
        ISecEventParseRuleCatalogCache cache,
        ILogger<SecEventCatalogParseEngine> logger)
    {
        _catalog = catalog;
        _cache = cache;
        _logger = logger;
    }

    public async Task<ParsedSecEvent?> TryParseAsync(
        string domain,
        SecEventRawContext context,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(domain))
            return null;

        try
        {
            await _catalog.EnsureCatalogReadyAsync(domain, cancellationToken);
            var rules = await _cache.GetEnabledRulesAsync(domain, cancellationToken);
            if (rules.Count == 0)
                return null;

            foreach (var rule in rules)
            {
                if (!Matches(rule, context))
                    continue;

                var fields = ApplyExtract(rule, context);
                if (!fields.TryGetValue("event.action", out var actionObj)
                    || actionObj is null
                    || string.IsNullOrWhiteSpace(actionObj.ToString())
                    || string.Equals(actionObj.ToString(), "unknown", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogDebug(
                        "Catalog rule {RuleId} matched but event.action missing/unknown; falling back",
                        rule.RuleId);
                    return null;
                }

                return ToParsed(rule, context, fields);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Catalog parse engine failed domain={Domain}; using code parsers", domain);
        }

        return null;
    }

    internal static bool Matches(SecEventParseRuleDocument rule, SecEventRawContext ctx)
    {
        var product = SecEventParseHelpers.NormalizeProduct(ctx.Source.Product);
        var type = SecEventParseHelpers.NormalizeType(ctx.Source.Type);
        if (!SecEventParseFieldResolver.MatchesSourceProduct(rule.Match.SourceProduct, product, type))
            return false;

        if (!SecEventParseFieldResolver.MatchesSourceType(rule.Match.SourceType, type))
            return false;

        if (rule.Match.Channel is { Count: > 0 })
        {
            var channel = SecEventParseFieldResolver.ReadChannel(ctx.Raw) ?? string.Empty;
            if (!string.IsNullOrEmpty(channel)
                && !rule.Match.Channel.Any(c => string.Equals(c, channel, StringComparison.OrdinalIgnoreCase)))
                return false;
        }

        if (rule.Match.EventIds is { Count: > 0 })
        {
            var eventId = SecEventParseFieldResolver.ReadEventId(ctx.Raw);
            if (!eventId.HasValue || !rule.Match.EventIds.Contains(eventId.Value))
                return false;
        }

        if (rule.Match.When is { Count: > 0 })
        {
            foreach (var when in rule.Match.When)
            {
                if (!EvaluateWhen(when, ctx))
                    return false;
            }
        }

        if (rule.Match.MessagePatterns is { Count: > 0 })
        {
            var text = SecEventParseHelpers.GetRawText(ctx.Raw);
            if (!rule.Match.MessagePatterns.Any(p => MessageFamilyMatches(text, p.Family)))
                return false;
        }

        return true;
    }

    private static bool MessageFamilyMatches(string text, string family)
    {
        if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(family))
            return false;

        return family.Trim().ToLowerInvariant() switch
        {
            "sshd_failed_password" => text.Contains("Failed password", StringComparison.OrdinalIgnoreCase),
            "sshd_accepted" => text.Contains("Accepted password", StringComparison.OrdinalIgnoreCase)
                               || text.Contains("Accepted publickey", StringComparison.OrdinalIgnoreCase),
            "sudo_command" => text.Contains("sudo:", StringComparison.OrdinalIgnoreCase)
                              && !text.Contains("command not allowed", StringComparison.OrdinalIgnoreCase),
            "sudo_not_allowed" => text.Contains("command not allowed", StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }

    private static bool EvaluateWhen(SecEventParseRuleWhen when, SecEventRawContext ctx)
    {
        var rawValue = ReadField(ctx, when.Field);
        var op = when.Op.Trim().ToLowerInvariant();
        return op switch
        {
            "exists" => !string.IsNullOrEmpty(rawValue),
            "eq" => string.Equals(rawValue, when.Value, StringComparison.OrdinalIgnoreCase),
            "neq" => !string.Equals(rawValue, when.Value, StringComparison.OrdinalIgnoreCase),
            "in" => when.Values?.Any(v => string.Equals(rawValue, v, StringComparison.OrdinalIgnoreCase)) == true,
            "contains" => !string.IsNullOrEmpty(when.Value)
                          && !string.IsNullOrEmpty(rawValue)
                          && rawValue.Contains(when.Value, StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }

    private static Dictionary<string, object?> ApplyExtract(
        SecEventParseRuleDocument rule,
        SecEventRawContext ctx)
    {
        var fields = new Dictionary<string, object?>(StringComparer.Ordinal);
        var rawText = SecEventParseHelpers.GetRawText(ctx.Raw);

        foreach (var step in rule.Extract)
        {
            switch (step.Type.ToLowerInvariant())
            {
                case "constant":
                    if (!string.IsNullOrWhiteSpace(step.To))
                        fields[step.To!] = Coerce(step.To!, step.Value);
                    break;
                case "event_data":
                case "json_path":
                {
                    var from = step.From ?? string.Empty;
                    string? value;
                    if (step.Type.Equals("event_data", StringComparison.OrdinalIgnoreCase))
                        value = SecEventParseFieldResolver.ReadEventData(ctx.Raw, from);
                    else
                        value = ReadField(ctx, from)
                                ?? SecEventParseFieldResolver.ReadPath(ctx.Raw, $"fields.{from}");
                    if (value is not null && !string.IsNullOrWhiteSpace(step.To))
                        fields[step.To!] = Coerce(step.To!, value);
                    break;
                }
                case "regex":
                {
                    var input = string.IsNullOrWhiteSpace(step.From) || step.From == "message"
                        ? (SecEventParseFieldResolver.ReadMessage(ctx.Raw) ?? rawText)
                        : ReadField(ctx, step.From!) ?? rawText;
                    if (string.IsNullOrEmpty(input) || string.IsNullOrEmpty(step.Pattern) || step.Groups is null)
                        break;
                    var match = Regex.Match(
                        input,
                        step.Pattern,
                        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
                        TimeSpan.FromMilliseconds(250));
                    if (!match.Success)
                        break;
                    foreach (var (groupKey, target) in step.Groups)
                    {
                        string? g = null;
                        if (int.TryParse(groupKey, out var idx))
                        {
                            if (idx >= 0 && idx < match.Groups.Count)
                                g = match.Groups[idx].Value;
                        }
                        else if (match.Groups[groupKey].Success)
                        {
                            g = match.Groups[groupKey].Value;
                        }

                        if (g is not null)
                            fields[target] = Coerce(target, g);
                    }

                    break;
                }
                case "kv":
                {
                    if (step.Groups is { Count: > 0 })
                    {
                        foreach (var (key, target) in step.Groups)
                        {
                            var v = ReadKv(rawText, key);
                            if (v is not null)
                                fields[target] = Coerce(target, v);
                        }
                    }
                    else if (!string.IsNullOrWhiteSpace(step.From) && !string.IsNullOrWhiteSpace(step.To))
                    {
                        var v = ReadKv(rawText, step.From!);
                        if (v is not null)
                            fields[step.To!] = Coerce(step.To!, v);
                    }

                    break;
                }
            }
        }

        return fields;
    }

    private static ParsedSecEvent ToParsed(
        SecEventParseRuleDocument rule,
        SecEventRawContext ctx,
        Dictionary<string, object?> fields)
    {
        var rawText = SecEventParseHelpers.GetRawText(ctx.Raw);
        var eventId = SecEventParseFieldResolver.ReadEventId(ctx.Raw);
        var timestamp = ReadTimestamp(ctx) ?? ctx.ReceivedAt;

        var sourceTypeDefault = rule.Match.SourceType?.FirstOrDefault()
                                ?? (rule.Match.SourceProduct.Any(p =>
                                    p.Contains("linux", StringComparison.OrdinalIgnoreCase))
                                    ? "endpoint"
                                    : "ad");
        var sourceProductDefault = rule.Match.SourceProduct.FirstOrDefault() ?? "unknown";

        return new ParsedSecEvent
        {
            Timestamp = timestamp,
            EventAction = fields["event.action"]!.ToString()!,
            EventOutcome = fields.GetValueOrDefault("event.outcome")?.ToString(),
            EventCode = eventId?.ToString() ?? fields.GetValueOrDefault("event.code")?.ToString(),
            ActorUser = fields.GetValueOrDefault("actor.user")?.ToString(),
            NetworkSrcIp = fields.GetValueOrDefault("network.srcIp")?.ToString(),
            NetworkDstIp = fields.GetValueOrDefault("network.dstIp")?.ToString(),
            NetworkDstPort = fields.GetValueOrDefault("network.dstPort") as int?
                             ?? (int.TryParse(fields.GetValueOrDefault("network.dstPort")?.ToString(), out var p)
                                 ? p
                                 : null),
            NetworkProtocol = fields.GetValueOrDefault("network.protocol")?.ToString(),
            SourceType = SecEventParseHelpers.ResolveSourceType(ctx.Source, sourceTypeDefault),
            SourceProduct = SecEventParseHelpers.ResolveSourceProduct(ctx.Source, sourceProductDefault),
            SourceHost = ctx.Source.Host,
            ParserId = rule.RuleId,
            Raw = SecEventParseHelpers.ToStoredRaw(rawText),
            RawPreview = SecEventParseHelpers.ToRawPreview(rawText),
            ExtraFields = CollectExtraFields(fields)
        };
    }

    private static IReadOnlyDictionary<string, object?> CollectExtraFields(
        Dictionary<string, object?> fields)
    {
        var extras = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var (key, value) in fields)
        {
            if (value is null)
                continue;
            if (SecEventTargetFieldCatalog.IsCustomField(key)
                || string.Equals(key, "message", StringComparison.Ordinal)
                || string.Equals(key, "tags", StringComparison.Ordinal)
                || string.Equals(key, "event.category", StringComparison.Ordinal)
                || string.Equals(key, "event.severity", StringComparison.Ordinal))
            {
                extras[key] = value;
            }
        }

        return extras;
    }

    private static object? Coerce(string target, string? value)
    {
        if (value is null)
            return null;
        if (target == "network.dstPort" && int.TryParse(value, out var port))
            return port;
        if (target == "event.severity" && int.TryParse(value, out var sev))
            return sev;
        return value;
    }

    private static string? ReadField(SecEventRawContext ctx, string field)
    {
        if (string.Equals(field, "message", StringComparison.OrdinalIgnoreCase))
            return SecEventParseFieldResolver.ReadMessage(ctx.Raw)
                   ?? SecEventParseHelpers.GetRawText(ctx.Raw);

        if (ctx.Raw.ValueKind != JsonValueKind.Object)
            return null;

        return SecEventParseFieldResolver.ReadPath(ctx.Raw, field)
               ?? SecEventParseFieldResolver.ReadPath(ctx.Raw, $"fields.{field}");
    }

    private static DateTime? ReadTimestamp(SecEventRawContext ctx)
    {
        var text = ReadField(ctx, "TimeCreated")
                   ?? ReadField(ctx, "EventTime")
                   ?? ReadField(ctx, "timeCreated")
                   ?? ReadField(ctx, "@timestamp");
        if (string.IsNullOrWhiteSpace(text))
            return null;
        if (DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt))
            return dt.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(dt, DateTimeKind.Utc) : dt.ToUniversalTime();
        return null;
    }

    private static string? ReadKv(string line, string key)
    {
        if (string.IsNullOrEmpty(line) || string.IsNullOrEmpty(key))
            return null;
        var token = key + "=";
        var idx = line.IndexOf(token, StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
            return null;
        var start = idx + token.Length;
        var end = start;
        while (end < line.Length && !char.IsWhiteSpace(line[end]))
            end++;
        return line[start..end];
    }
}
