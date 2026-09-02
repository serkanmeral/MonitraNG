using System.Text.Json;
using MngOperations.Application.Contracts.Planning;

namespace MngOperations.Application.Packs;

public static class JobPackCatalog
{
    private static readonly Lazy<IReadOnlyList<JobPackDefinition>> Packs = new(Load);

    public static IReadOnlyList<JobPackDefinition> All => Packs.Value;

    public static JobPackDefinition? Find(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;
        return All.FirstOrDefault(p => string.Equals(p.Code, code.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    public static JobPackDto ToDto(JobPackDefinition pack) => new()
    {
        Code = pack.Code,
        Name = pack.Name,
        Version = NormalizeVersion(pack.Version),
        Description = pack.Description,
        Kinds = pack.Kinds,
        Folders = pack.Folders.Select(f => f.Name).ToList(),
        Wbs = pack.Wbs.Select(ToPreview).ToList(),
        Starters = pack.Starters.Select(s => new JobPackStarterDto
        {
            Folder = s.Folder,
            Title = s.Title,
            Kind = s.Kind,
            Body = s.Body
        }).ToList()
    };

    public static string NormalizeVersion(string? version)
    {
        var v = version?.Trim();
        return string.IsNullOrWhiteSpace(v) ? "1.0.0" : v;
    }

    private static JobPackWbsPreview ToPreview(JobPackWbsNode node) => new()
    {
        Name = node.Name,
        Kind = node.Kind,
        Children = (node.Children ?? []).Select(ToPreview).ToList()
    };

    private static IReadOnlyList<JobPackDefinition> Load()
    {
        var asm = typeof(JobPackCatalog).Assembly;
        var names = asm.GetManifestResourceNames()
            .Where(n => n.Contains(".Packs.", StringComparison.Ordinal) && n.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var list = new List<JobPackDefinition>();
        foreach (var name in names)
        {
            using var stream = asm.GetManifestResourceStream(name);
            if (stream is null) continue;
            var pack = JsonSerializer.Deserialize<JobPackDefinition>(stream, JobPackJson.Options);
            if (pack is null || string.IsNullOrWhiteSpace(pack.Code)) continue;
            list.Add(pack);
        }

        return list
            .OrderBy(p => p.Code, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
