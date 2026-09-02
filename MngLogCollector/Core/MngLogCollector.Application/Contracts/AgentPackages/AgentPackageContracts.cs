namespace MngLogCollector.Application.Contracts.AgentPackages;

public sealed class AgentPackageCatalogResponse
{
    public string CollectorBaseUrl { get; set; } = string.Empty;

    public IReadOnlyList<AgentPackageDto> Packages { get; set; } = [];
}

public sealed class AgentPackageDto
{
    public string Id { get; set; } = string.Empty;

    public string Platform { get; set; } = string.Empty;

    public string FileName { get; set; } = string.Empty;

    public string Version { get; set; } = string.Empty;

    public string Sha256 { get; set; } = string.Empty;

    public long SizeBytes { get; set; }

    public string DownloadPath { get; set; } = string.Empty;

    public string DownloadUrl { get; set; } = string.Empty;
}

public sealed class AgentPackageFile
{
    public string Id { get; set; } = string.Empty;

    public string FileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = "application/octet-stream";

    public string AbsolutePath { get; set; } = string.Empty;
}
