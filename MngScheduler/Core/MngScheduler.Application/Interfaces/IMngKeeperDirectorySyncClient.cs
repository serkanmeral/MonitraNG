namespace MngScheduler.Application.Interfaces;

public interface IMngKeeperDirectorySyncClient
{
    Task<MngKeeperDirectorySyncResponse> TriggerScheduledSyncAsync(
        string domainIdOrRealm,
        Dictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default);
}

public class MngKeeperDirectorySyncResponse
{
    public int StatusCode { get; set; }
    public bool IsSkipped => StatusCode == 409;
    public bool IsSuccess => StatusCode >= 200 && StatusCode < 300;
    public string? Code { get; set; }
    public string? Message { get; set; }
    public string? RawBody { get; set; }
}
