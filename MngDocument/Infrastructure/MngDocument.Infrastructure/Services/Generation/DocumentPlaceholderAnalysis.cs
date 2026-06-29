using MngDocument.Infrastructure.Services;

namespace MngDocument.Infrastructure.Services.Generation;

internal static class DocumentPlaceholderAnalysis
{
    internal sealed record Result(
        IReadOnlyList<string> UndefinedParameterKeys,
        IReadOnlyList<string> UnresolvedParameterKeys,
        IReadOnlySet<string> PreservePlaceholderKeys);

    internal static Result Analyze(
        DocxPlaceholderScanner.ScanResult scan,
        TemplateModelDocument model,
        IReadOnlyDictionary<string, string> values)
    {
        var modelKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var param in model.Parameters)
        {
            var key = param.Key?.Trim();
            if (!string.IsNullOrWhiteSpace(key))
                modelKeys.Add(key);
        }

        var docKeys = scan.Placeholders
            .Select(p => p.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var undefined = docKeys
            .Where(k => !modelKeys.Contains(k))
            .OrderBy(k => k, StringComparer.Ordinal)
            .ToList();

        var unresolved = modelKeys
            .Where(k => docKeys.Contains(k) && IsEmptyValue(values, k))
            .OrderBy(k => k, StringComparer.Ordinal)
            .ToList();

        var preserve = undefined
            .Concat(unresolved)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return new Result(undefined, unresolved, preserve);
    }

    internal static IReadOnlyList<string> ScanRemainingPlaceholderKeys(byte[] docxBytes)
    {
        using var stream = new MemoryStream(docxBytes, writable: false);
        var scan = DocxPlaceholderScanner.Scan(stream);
        return scan.Placeholders
            .Select(p => p.Key)
            .OrderBy(k => k, StringComparer.Ordinal)
            .ToList();
    }

    private static bool IsEmptyValue(IReadOnlyDictionary<string, string> values, string key)
    {
        if (!values.TryGetValue(key, out var value))
            return true;

        return string.IsNullOrWhiteSpace(value);
    }
}
