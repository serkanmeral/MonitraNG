using MongoDB.Bson.Serialization.Attributes;

namespace MngScheduler.Domain.Entities;

/// <summary>
/// Job execution history entity
/// </summary>
public class JobExecution
{
    [BsonId]
    [BsonRepresentation(MongoDB.Bson.BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Unique execution identifier
    /// </summary>
    [BsonElement("executionId")]
    public string ExecutionId { get; set; } = string.Empty;

    /// <summary>
    /// Job ID
    /// </summary>
    [BsonElement("jobId")]
    public string JobId { get; set; } = string.Empty;

    /// <summary>
    /// Execution status (success, failed, timeout)
    /// </summary>
    [BsonElement("status")]
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Execution timestamp
    /// </summary>
    [BsonElement("executedAt")]
    public DateTime ExecutedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Response time in milliseconds
    /// </summary>
    [BsonElement("responseTimeMs")]
    public long ResponseTimeMs { get; set; }

    /// <summary>
    /// HTTP response code
    /// </summary>
    [BsonElement("responseCode")]
    public int? ResponseCode { get; set; }

    /// <summary>
    /// Response body
    /// </summary>
    [BsonElement("responseBody")]
    public string? ResponseBody { get; set; }

    /// <summary>
    /// Error message (if failed)
    /// </summary>
    [BsonElement("errorMessage")]
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Retry count
    /// </summary>
    [BsonElement("retryCount")]
    public int RetryCount { get; set; } = 0;

    /// <summary>
    /// Domain ID (for User jobs)
    /// </summary>
    [BsonElement("domainId")]
    public string? DomainId { get; set; }
}
