using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using MngDocument.Application.Contracts.Generation;

namespace MngDocument.Infrastructure.Services.Generation.DataSources;

internal static partial class DataSourceTokenResolver
{
    private static readonly Regex TokenRegex = TokenPattern();

    public static string ResolveString(string? template, ParameterResolutionContext context)
    {
        if (string.IsNullOrWhiteSpace(template))
            return string.Empty;

        return TokenRegex.Replace(template, match =>
        {
            var key = match.Groups[1].Value.Trim();
            var value = ResolveToken(key, context);
            return value ?? match.Value;
        });
    }

    public static object? ResolveValue(object? value, ParameterResolutionContext context)
    {
        if (value is JsonElement element)
            return ResolveJsonElement(element, context);

        if (value is JsonNode node)
            return ResolveJsonNode(node, context);

        if (value is string s)
            return ResolveString(s, context);

        if (value is JsonValue jv && jv.TryGetValue<string>(out var js))
            return ResolveString(js, context);

        return value;
    }

    public static Dictionary<string, object?> ResolveMatch(
        IReadOnlyDictionary<string, object?>? match,
        ParameterResolutionContext context)
    {
        if (match is null || match.Count == 0)
            return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in match)
            result[kv.Key] = ResolveValue(kv.Value, context);

        return result;
    }

    private static object? ResolveJsonElement(JsonElement element, ParameterResolutionContext context) =>
        element.ValueKind switch
        {
            JsonValueKind.String => ResolveString(element.GetString(), context),
            JsonValueKind.Number when element.TryGetInt64(out var l) => l,
            JsonValueKind.Number when element.TryGetDouble(out var d) => d,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            JsonValueKind.Object => ResolveJsonObject(element, context),
            JsonValueKind.Array => element.EnumerateArray()
                .Select(item => ResolveJsonElement(item, context))
                .ToList(),
            _ => element.GetRawText()
        };

    private static object? ResolveJsonNode(JsonNode? node, ParameterResolutionContext context)
    {
        if (node is null)
            return null;

        if (node is JsonValue val)
        {
            if (val.TryGetValue<string>(out var s))
                return ResolveString(s, context);
            return val.TryGetValue<object>(out var raw) ? raw : val.ToString();
        }

        if (node is JsonObject obj)
        {
            var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in obj)
                dict[kv.Key] = ResolveJsonNode(kv.Value, context);
            return dict;
        }

        if (node is JsonArray array)
            return array.Select(item => ResolveJsonNode(item, context)).ToList();

        return node.ToString();
    }

    private static Dictionary<string, object?> ResolveJsonObject(
        JsonElement element,
        ParameterResolutionContext context)
    {
        var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var prop in element.EnumerateObject())
            dict[prop.Name] = ResolveJsonElement(prop.Value, context);
        return dict;
    }

    private static string? ResolveToken(string key, ParameterResolutionContext context)
    {
        if (key.StartsWith("runtime.", StringComparison.OrdinalIgnoreCase))
        {
            return key.ToLowerInvariant() switch
            {
                "runtime.contextid" => context.ContextId,
                "runtime.contexttype" => context.ContextType,
                "runtime.userid" => context.UserId,
                _ => null
            };
        }

        if (key.StartsWith("scope.", StringComparison.OrdinalIgnoreCase))
        {
            return key.ToLowerInvariant() switch
            {
                "scope.workspaceid" => context.WorkspaceId,
                "scope.domainid" => context.DomainId,
                _ => null
            };
        }

        if (key.StartsWith("params.", StringComparison.OrdinalIgnoreCase))
        {
            var paramKey = key["params.".Length..];
            return context.Params.TryGetValue(paramKey, out var v) ? v : null;
        }

        if (key.StartsWith("context.", StringComparison.OrdinalIgnoreCase))
        {
            var path = key["context.".Length..];
            return DocumentContextPathResolver.GetString(context.ContextTree, path);
        }

        return null;
    }

    [GeneratedRegex(@"\{\{\s*([^}]+?)\s*\}\}", RegexOptions.CultureInvariant)]
    private static partial Regex TokenPattern();
}
