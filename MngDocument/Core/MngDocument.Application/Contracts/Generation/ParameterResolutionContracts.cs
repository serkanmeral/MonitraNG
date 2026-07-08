namespace MngDocument.Application.Contracts.Generation;

/// <summary>Resolved template parameters — scalars for merge; tables for region expanders (G2+).</summary>
public sealed class ParameterResolutionResult
{
    public Dictionary<string, string> Scalars { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, IReadOnlyList<IReadOnlyDictionary<string, object?>>> Tables { get; init; }
        = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>Runtime context passed to parameter and data source resolvers.</summary>
public sealed class ParameterResolutionContext
{
    public string ContextId { get; init; } = string.Empty;
    public string ContextType { get; init; } = string.Empty;
    public System.Text.Json.Nodes.JsonObject ContextTree { get; init; } = new();
    public string? WorkspaceId { get; init; }
    public string? DomainId { get; init; }
    public string? UserId { get; init; }
    public IReadOnlyDictionary<string, string> Params { get; init; }
        = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}
