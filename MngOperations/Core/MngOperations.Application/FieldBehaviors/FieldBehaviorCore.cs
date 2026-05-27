using System.Text.Json;
using MngOperations.Application.Contracts.Runtime;
using MngOperations.Application.Models;

namespace MngOperations.Application.FieldBehaviors;

public enum FieldBehaviorScreen
{
    Form,
    Profile
}

public sealed record FieldBehaviorResolveContext
{
    public required FieldBehaviorScreen Screen { get; init; }
    public required string Mode { get; init; }
    public required WorkspaceRecord Workspace { get; init; }
    public required IReadOnlyDictionary<string, object?> WorkItem { get; init; }
    public FormRecord? Form { get; init; }
    public ProfileRecord? Profile { get; init; }
    public BoardRecord? Board { get; init; }
    public string? StateId { get; init; }
    public bool CanEdit { get; init; }
    public string RuleTrigger { get; init; } = "WorkItemUpdated";
}

public static class FieldBehaviorMerger
{
    public static FieldBehaviorDto Merge(FieldBehaviorDto current, FieldBehaviorDto layer) =>
        new()
        {
            Visible = current.Visible && layer.Visible,
            Readonly = current.Readonly || layer.Readonly,
            Required = current.Required || layer.Required,
            Masked = current.Masked || layer.Masked
        };

    public static FieldBehaviorDto MergeMany(IEnumerable<FieldBehaviorDto> layers)
    {
        var result = new FieldBehaviorDto
        {
            Visible = true,
            Readonly = false,
            Required = false,
            Masked = false
        };

        foreach (var layer in layers)
            result = Merge(result, layer);

        return result;
    }
}

public static class FieldBehaviorParser
{
    public static FieldBehaviorDto ParseObject(JsonElement element, FieldBehaviorDto? fallback = null)
    {
        fallback ??= new FieldBehaviorDto
        {
            Visible = true,
            Readonly = false,
            Required = false,
            Masked = false
        };

        return new FieldBehaviorDto
        {
            Visible = ReadBool(element, "visible", fallback.Visible),
            Readonly = ReadBool(element, "readonly", fallback.Readonly)
                       || ReadBool(element, "readOnly", fallback.Readonly),
            Required = ReadBool(element, "required", fallback.Required),
            Masked = ReadBool(element, "masked", fallback.Masked)
        };
    }

    public static IReadOnlyDictionary<string, FieldBehaviorDto> ParseMap(JsonElement? behaviors)
    {
        var result = new Dictionary<string, FieldBehaviorDto>(StringComparer.OrdinalIgnoreCase);
        if (behaviors is not { ValueKind: JsonValueKind.Object })
            return result;

        foreach (var prop in behaviors.Value.EnumerateObject())
        {
            if (prop.Value.ValueKind == JsonValueKind.Object)
                result[prop.Name] = ParseObject(prop.Value);
        }

        return result;
    }

    private static bool ReadBool(JsonElement element, string name, bool defaultValue)
    {
        if (!element.TryGetProperty(name, out var prop))
            return defaultValue;

        return prop.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => defaultValue
        };
    }
}

public static class FieldBehaviorDefaults
{
    public static readonly string[] SystemFieldKeys =
        ["title", "description", "typeId", "assignee", "priorityId", "boardId"];

    public static IReadOnlyDictionary<string, FieldBehaviorDto> ForSystemFields(string mode)
    {
        var isEdit = string.Equals(mode, "edit", StringComparison.OrdinalIgnoreCase);
        var map = new Dictionary<string, FieldBehaviorDto>(StringComparer.OrdinalIgnoreCase)
        {
            ["title"] = new() { Visible = true, Required = true },
            ["description"] = new() { Visible = true },
            ["typeId"] = new() { Visible = true, Required = true, Readonly = isEdit },
            ["assignee"] = new() { Visible = true },
            ["priorityId"] = new() { Visible = true },
            ["boardId"] = new() { Visible = true }
        };

        return map;
    }
}
