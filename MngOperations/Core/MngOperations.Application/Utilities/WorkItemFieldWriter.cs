using MngOperations.Application.Exceptions;
using MngOperations.Application.Models;

namespace MngOperations.Application.Utilities;

public static class WorkItemFieldWriter
{
    public static void Apply(
        Dictionary<string, object?> target,
        IReadOnlyDictionary<string, object?> incoming,
        IReadOnlyDictionary<string, FieldRecord> enabledPoolFieldsByKey)
    {
        if (incoming.Count == 0)
            return;

        var extraFields = WorkItemDataHelper.CloneExtraFieldsDictionary(target);

        foreach (var (key, value) in incoming)
        {
            if (WorkItemCoreFields.IsReserved(key))
            {
                throw new OperationCoreException(
                    "RESERVED_FIELD",
                    $"Field '{key}' cannot be modified.",
                    $"'{key}' alanı değiştirilemez.",
                    400);
            }

            if (WorkItemCoreFields.IsWritable(key))
            {
                target[key] = value;
                extraFields.Remove(key);
                continue;
            }

            if (enabledPoolFieldsByKey.ContainsKey(key))
            {
                if (IsEmptyValue(value))
                    extraFields.Remove(key);
                else
                    extraFields[key] = value;

                target.Remove(key);
                continue;
            }

            throw new OperationCoreException(
                "UNKNOWN_FIELD",
                $"Field '{key}' is not defined or not allowed.",
                $"'{key}' alanı tanımsız veya kullanılamaz.",
                400);
        }

        if (extraFields.Count == 0)
            target.Remove(WorkItemCoreFields.ExtraFieldsKey);
        else
            target[WorkItemCoreFields.ExtraFieldsKey] = extraFields;
    }

    private static bool IsEmptyValue(object? value) =>
        value switch
        {
            null => true,
            string s => HtmlRichTextHelper.IsEffectivelyEmptyHtml(s),
            _ => false
        };
}
