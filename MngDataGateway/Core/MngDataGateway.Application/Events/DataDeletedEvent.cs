namespace MngDataGateway.Application.Events;

/// <summary>
/// Event published when data is deleted from a dataset
/// </summary>
public class DataDeletedEvent : BaseDataEvent
{
    /// <summary>
    /// Dataset name (e.g., "@books")
    /// </summary>
    public string DatasetName { get; set; } = string.Empty;

    /// <summary>
    /// Deleted data ID (__dataId)
    /// </summary>
    public string DataId { get; set; } = string.Empty;

    /// <summary>
    /// User ID who deleted the data
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// User email who deleted the data
    /// </summary>
    public string UserEmail { get; set; } = string.Empty;

    /// <summary>
    /// IP address of the user
    /// </summary>
    public string? IpAddress { get; set; }
}

