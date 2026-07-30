namespace MngLogCollector.Application.Contracts.Policy;

public sealed class EventLogPackageDto
{
    public string Name { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public List<int> EventIds { get; set; } = [];
}

public sealed class EventLogPackageCatalogResponse
{
    public string Version { get; set; } = string.Empty;
    public string Source { get; set; } = "collector";
    public DateTime GeneratedUtc { get; set; }
    public List<EventLogPackageDto> Packages { get; set; } = [];
    public List<EventLogPackageDto> OptionalPackages { get; set; } = [];
}
