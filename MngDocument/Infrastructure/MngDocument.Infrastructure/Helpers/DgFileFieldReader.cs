using System.Text.Json;
using MngDocument.Application.Models;

namespace MngDocument.Infrastructure.Helpers;

internal static class DgFileFieldReader
{
    internal static (string? Path, string? Name) Read(JsonElement? file)
    {
        if (file is null || file.Value.ValueKind != JsonValueKind.Object)
            return (null, null);

        string? path = file.Value.TryGetProperty("path", out var p) && p.ValueKind == JsonValueKind.String
            ? p.GetString()
            : null;
        string? name = file.Value.TryGetProperty("file_name", out var n) && n.ValueKind == JsonValueKind.String
            ? n.GetString()
            : null;

        return (string.IsNullOrWhiteSpace(path) ? null : path, string.IsNullOrWhiteSpace(name) ? null : name);
    }

    internal static (string? Path, string? Name) Read(DmDocumentTemplate row) =>
        Read(row.referenceFile);
}
