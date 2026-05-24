namespace MngScheduler.Application.Interfaces;

/// <summary>
/// K3 — runs directory sync for all active domains (Keeper POST per domain).
/// </summary>
public interface IDirectorySyncOrchestrationService
{
    Task<DirectorySyncOrchestrationResult> RunAsync(
        Dictionary<string, string>? requestHeaders = null,
        CancellationToken cancellationToken = default);
}

public class DirectorySyncOrchestrationResult
{
    public bool IsSuccess { get; set; }
    public int DomainsTotal { get; set; }
    public int DomainsSucceeded { get; set; }
    public int DomainsSkipped { get; set; }
    public int DomainsFailed { get; set; }
    public long DurationMs { get; set; }
    public string Summary { get; set; } = string.Empty;
    public List<DirectorySyncDomainAttempt> Domains { get; set; } = new();
}

public class DirectorySyncDomainAttempt
{
    public string DomainId { get; set; } = string.Empty;
    public string DomainName { get; set; } = string.Empty;
    public string KeeperDomainKey { get; set; } = string.Empty;
    public string Outcome { get; set; } = string.Empty;
    public int? HttpStatusCode { get; set; }
    public string? Code { get; set; }
    public string? Message { get; set; }
}
