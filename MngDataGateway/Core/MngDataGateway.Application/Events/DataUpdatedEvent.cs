namespace MngDataGateway.Application.Events;

/// <summary>
/// Event published when data is updated in a dataset
/// </summary>
public class DataUpdatedEvent : BaseDataEvent
{
    /// <summary>
    /// Dataset name (e.g., "@books")
    /// </summary>
    public string DatasetName { get; set; } = string.Empty;

    /// <summary>
    /// Updated data ID (__dataId)
    /// </summary>
    public string DataId { get; set; } = string.Empty;

    /// <summary>
    /// Updated data (full object)
    /// </summary>
    public object Data { get; set; } = null!;

    /// <summary>
    /// User ID who updated the data
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// User email who updated the data
    /// </summary>
    public string UserEmail { get; set; } = string.Empty;

    /// <summary>
    /// IP address of the user
    /// </summary>
    public string? IpAddress { get; set; }
}

