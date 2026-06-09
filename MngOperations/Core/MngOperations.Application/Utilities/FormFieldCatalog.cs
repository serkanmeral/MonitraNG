using MngOperations.Application.Models;

namespace MngOperations.Application.Utilities;

public sealed record FormFieldDefinition(string Key, string Label, string FieldType, bool IsPool);

public static class FormFieldCatalog
{
    private static readonly FormFieldDefinition[] CoreDefinitions =
    [
        new("title", "Title", "text", false),
        new("description", "Description", "richtext", false),
        new("typeId", "Type", "relation", false),
        new("assignee", "Assignee", "persons", false),
        new("priorityId", "Priority", "relation", false),
        new("boardId", "Board", "relation", false),
        new("impact", "Impact", "text", false),
        new("urgency", "Urgency", "text", false),
        new("severity", "Severity", "text", false),
        new("reporter", "Reporter", "persons", false),
        new("watchers", "Watchers", "persons", false),
        new("dueDate", "Due date", "datetime", false),
        new("labels", "Labels", "relation", false),
        new("parentItemId", "Parent item", "relation", false)
    ];

    public static IReadOnlyDictionary<string, FormFieldDefinition> BuildCoreByKey() =>
        CoreDefinitions.ToDictionary(d => d.Key, StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyDictionary<string, FormFieldDefinition> MergeCoreAndPool(
        IReadOnlyDictionary<string, FieldRecord> poolFieldsByKey)
    {
        var map = new Dictionary<string, FormFieldDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var def in CoreDefinitions)
            map[def.Key] = def;

        foreach (var (key, field) in poolFieldsByKey)
        {
            if (map.TryGetValue(key, out var existing) && !existing.IsPool)
            {
                // Core şema alanı — havuz kaydı yalnızca etiket özelleştirir; fieldType sabit kalır.
                var label = string.IsNullOrWhiteSpace(field.Label) ? existing.Label : field.Label!;
                map[key] = new FormFieldDefinition(key, label, existing.FieldType, IsPool: false);
                continue;
            }

            map[key] = new FormFieldDefinition(
                key,
                string.IsNullOrWhiteSpace(field.Label) ? key : field.Label!,
                string.IsNullOrWhiteSpace(field.FieldType) ? "text" : field.FieldType!,
                IsPool: true);
        }

        return map;
    }
}
