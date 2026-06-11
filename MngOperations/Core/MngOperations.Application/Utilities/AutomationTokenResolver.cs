using System.Text.RegularExpressions;
using MngOperations.Application.Contracts.Automations;

namespace MngOperations.Application.Utilities;

public static partial class AutomationTokenResolver
{
    private static readonly Regex TokenRegex = TokenPattern();

    public static string Resolve(string? template, WorkspaceAutomationTriggerContext context)
    {
        if (string.IsNullOrWhiteSpace(template))
            return string.Empty;

        return TokenRegex.Replace(template, match =>
        {
            var path = match.Groups[1].Value.Trim();
            var value = ResolvePath(path, context);
            return value ?? match.Value;
        });
    }

    public static string? ResolvePath(string path, WorkspaceAutomationTriggerContext context)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        var normalized = path.Trim();

        if (normalized.StartsWith("source.", StringComparison.OrdinalIgnoreCase))
        {
            var remainder = normalized["source.".Length..];
            return remainder.ToLowerInvariant() switch
            {
                "id" => context.WorkItemId,
                "key" => context.WorkItemKey,
                "assignee" => GetWorkItemString(context.WorkItem, "assignee"),
                _ when remainder.StartsWith("fields.", StringComparison.OrdinalIgnoreCase) =>
                    ResolveFieldPath(context.WorkItem, remainder),
                _ => ResolveFieldPath(context.WorkItem, remainder)
            };
        }

        if (normalized.StartsWith("event.", StringComparison.OrdinalIgnoreCase))
        {
            var remainder = normalized["event.".Length..];
            return remainder.ToLowerInvariant() switch
            {
                "transitionkey" => context.TransitionKey,
                "fromstateid" => context.FromStateId,
                "tostateid" => context.ToStateId,
                _ => null
            };
        }

        return null;
    }

    private static string? ResolveFieldPath(
        IReadOnlyDictionary<string, object?> workItem,
        string path)
    {
        if (path.StartsWith("fields.", StringComparison.OrdinalIgnoreCase))
        {
            var key = path["fields.".Length..];
            var value = WorkItemDataHelper.GetFieldValue(workItem, key);
            return FormatValue(value);
        }

        return FormatValue(GetWorkItemString(workItem, path));
    }

    private static string? GetWorkItemString(IReadOnlyDictionary<string, object?> workItem, string key)
    {
        if (!workItem.TryGetValue(key, out var value) || value is null)
            return null;

        return FormatValue(value);
    }

    private static string? FormatValue(object? value)
    {
        if (value is null)
            return null;

        return value switch
        {
            string s => s,
            bool b => b ? "true" : "false",
            _ => value.ToString()
        };
    }

    [GeneratedRegex(@"\{\{([^}]+)\}\}", RegexOptions.Compiled)]
    private static partial Regex TokenPattern();
}
