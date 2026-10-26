namespace MngOperations.Application.Contracts.WorkspacePacks;

public sealed class InstallWorkspacePackRequest
{
    public string Code { get; set; } = string.Empty;

    /// <summary>Yeni sürüm yalnızca bu işaret açıkken uygulanır.</summary>
    public bool Upgrade { get; set; }

    /// <summary>Pano anahtarı → görünen ad. Boş değer paketin kendi adını kullanır.</summary>
    public Dictionary<string, string>? BoardNames { get; set; }
}

public sealed class WorkspacePackInstallDto
{
    public string WorkspaceId { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public List<string> Created { get; set; } = [];
    public List<string> Skipped { get; set; } = [];
    public List<string> Left { get; set; } = [];
    public Dictionary<string, string> Pieces { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
