using System.Text.Json;

namespace MngOperations.Application.WorkspacePacks;

public static class WorkspacePackCatalog
{
    private static readonly Lazy<IReadOnlyList<WorkspacePackDefinition>> Packs = new(Load);

    public static IReadOnlyList<WorkspacePackDefinition> All => Packs.Value;

    public static WorkspacePackDefinition? Find(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;
        return All.FirstOrDefault(p => string.Equals(p.Code, code.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    public static string NormalizeVersion(string? version)
    {
        var value = version?.Trim();
        return string.IsNullOrWhiteSpace(value) ? "1.0.0" : value;
    }

    public static WorkspacePackSummaryDto ToSummary(WorkspacePackDefinition pack) => new()
    {
        Code = pack.Code,
        Name = pack.Name,
        Version = NormalizeVersion(pack.Version),
        Description = pack.Description,
        SuggestedWorkspaceType = pack.SuggestedWorkspaceType,
        SuggestedPrefix = pack.SuggestedPrefix,
        TypeCount = pack.Types.Count,
        StateCount = pack.States.Count,
        FlowCount = pack.Flows.Count,
        CanApply = pack.CanApply,
        Boards = pack.Boards
            .Select(board => new WorkspacePackBoardNameDto { Key = board.Key, Name = board.Name })
            .ToList()
    };

    private static IReadOnlyList<WorkspacePackDefinition> Load()
    {
        var asm = typeof(WorkspacePackCatalog).Assembly;
        var names = asm.GetManifestResourceNames()
            .Where(n => n.Contains(".WorkspacePacks.", StringComparison.Ordinal) && n.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var list = new List<WorkspacePackDefinition>();
        foreach (var name in names)
        {
            using var stream = asm.GetManifestResourceStream(name);
            if (stream is null) continue;
            var pack = JsonSerializer.Deserialize<WorkspacePackDefinition>(stream, WorkspacePackJson.Options);
            if (pack is null) continue;
            var errors = WorkspacePackValidator.Validate(pack);
            if (errors.Count > 0)
                throw new InvalidOperationException($"Workspace pack '{name}' is invalid: {string.Join(" ", errors)}");
            pack.CanApply = true;
            list.Add(pack);
        }

        return list
            .OrderBy(p => p.Code, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
