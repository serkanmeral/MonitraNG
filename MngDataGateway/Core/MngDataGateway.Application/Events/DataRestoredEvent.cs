namespace MngDataGateway.Application.Events;

/// <summary>
/// Event published when deleted data is restored
/// </summary>
public class DataRestoredEvent : BaseDataEvent
{
    /// <summary>
    /// Dataset name (e.g., "@books")
    /// </summary>
    public string DatasetName { get; set; } = string.Empty;

    /// <summary>
    /// Restored data ID (__dataId)
    /// </summary>
    public string DataId { get; set; } = string.Empty;

    /// <summary>
    /// User ID who restored the data
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// User email who restored the data
    /// </summary>
    public string UserEmail { get; set; } = string.Empty;

    /// <summary>
    /// IP address of the user
    /// </summary>
    public string? IpAddress { get; set; }
}

