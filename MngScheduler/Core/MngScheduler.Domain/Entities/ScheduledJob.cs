using MongoDB.Bson.Serialization.Attributes;

namespace MngScheduler.Domain.Entities;

/// <summary>
/// Scheduled job entity
/// </summary>
public class ScheduledJob
{
    [BsonId]
    [BsonRepresentation(MongoDB.Bson.BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Unique job identifier
    /// </summary>
    [BsonElement("jobId")]
    public string JobId { get; set; } = string.Empty;

    /// <summary>
    /// Job type (System or User)
    /// </summary>
    [BsonElement("jobType")]
    public JobType JobType { get; set; }

    /// <summary>
    /// Job name
    /// </summary>
    [BsonElement("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Job description
    /// </summary>
    [BsonElement("description")]
    public string? Description { get; set; }

    /// <summary>
    /// Cron expression for scheduling
    /// </summary>
    [BsonElement("cronExpression")]
    public string CronExpression { get; set; } = string.Empty;

    /// <summary>
    /// Endpoint URL to call
    /// </summary>
    [BsonElement("endpointUrl")]
    public string EndpointUrl { get; set; } = string.Empty;

    /// <summary>
    /// HTTP method (GET or POST)
    /// </summary>
    [BsonElement("httpMethod")]
    public string HttpMethod { get; set; } = "POST";

    /// <summary>
    /// HTTP headers
    /// </summary>
    [BsonElement("headers")]
    public Dictionary<string, string>? Headers { get; set; }

    /// <summary>
    /// Request payload (for POST requests) - JSON string
    /// If null or empty and HttpMethod is POST, default body will be used: {}
    /// </summary>
    [BsonElement("payload")]
    public string? Payload { get; set; }

    /// <summary>
    /// Default payload for POST requests (empty JSON object: {})
    /// </summary>
    private const string DefaultPostPayload = "{}";

    /// <summary>
    /// Is job active (manual control)
    /// </summary>
    [BsonElement("isActive")]
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Job start date/time (null = no start date restriction)
    /// Job will not execute before this date
    /// </summary>
    [BsonElement("startDate")]
    public DateTime? StartDate { get; set; }

    /// <summary>
    /// Job expiration date/time (null = no expiration)
    /// Job will not execute after this date
    /// </summary>
    [BsonElement("expireDate")]
    public DateTime? ExpireDate { get; set; }

    /// <summary>
    /// Maximum execution count (null = unlimited)
    /// Job will be deactivated after reaching this count
    /// </summary>
    [BsonElement("maxExecutionCount")]
    public int? MaxExecutionCount { get; set; }

    /// <summary>
    /// Total execution count (how many times the job has been executed - all attempts)
    /// This includes both successful and failed executions
    /// </summary>
    [BsonElement("totalExecutionCount")]
    public int TotalExecutionCount { get; set; } = 0;

    /// <summary>
    /// Successful execution count (how many times the job executed successfully)
    /// </summary>
    [BsonElement("successfulExecutionCount")]
    public int SuccessfulExecutionCount { get; set; } = 0;

    /// <summary>
    /// Failed execution count (how many times the job execution failed)
    /// </summary>
    [BsonElement("failedExecutionCount")]
    public int FailedExecutionCount { get; set; } = 0;

    /// <summary>
    /// Retry policy
    /// </summary>
    [BsonElement("retryPolicy")]
    public RetryPolicy? RetryPolicy { get; set; }

    /// <summary>
    /// Timeout in seconds
    /// </summary>
    [BsonElement("timeoutSeconds")]
    public int TimeoutSeconds { get; set; } = 300;

    /// <summary>
    /// Created timestamp
    /// </summary>
    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Updated timestamp
    /// </summary>
    [BsonElement("updatedAt")]
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// Created by (for User jobs)
    /// </summary>
    [BsonElement("createdBy")]
    public string? CreatedBy { get; set; }

    /// <summary>
    /// Domain ID (for User jobs)
    /// </summary>
    [BsonElement("domainId")]
    public string? DomainId { get; set; }

    /// <summary>
    /// Last execution information
    /// </summary>
    [BsonElement("lastExecution")]
    public JobExecution? LastExecution { get; set; }

    /// <summary>
    /// Determines if the job should be executed at the given time
    /// Checks: IsActive, StartDate, ExpireDate, MaxExecutionCount
    /// </summary>
    public bool ShouldExecute(DateTime? checkTime = null)
    {
        var now = checkTime ?? DateTime.UtcNow;

        // Manual deactivation
        if (!IsActive)
            return false;

        // StartDate check: if set, job should not execute before this date
        if (StartDate.HasValue && now < StartDate.Value)
            return false;

        // ExpireDate check: if set, job should not execute after this date
        if (ExpireDate.HasValue && now > ExpireDate.Value)
        {
            // Auto-deactivate if expired
            IsActive = false;
            return false;
        }

        // MaxExecutionCount check: if set, job should not execute if limit reached
        if (MaxExecutionCount.HasValue && TotalExecutionCount >= MaxExecutionCount.Value)
        {
            // Auto-deactivate if execution limit reached
            IsActive = false;
            return false;
        }

        return true;
    }

    /// <summary>
    /// Increments the total execution count
    /// </summary>
    public void IncrementTotalExecutionCount()
    {
        TotalExecutionCount++;
    }

    /// <summary>
    /// Increments the successful execution count
    /// </summary>
    public void IncrementSuccessfulExecutionCount()
    {
        SuccessfulExecutionCount++;
        IncrementTotalExecutionCount();
    }

    /// <summary>
    /// Increments the failed execution count
    /// </summary>
    public void IncrementFailedExecutionCount()
    {
        FailedExecutionCount++;
        IncrementTotalExecutionCount();
    }

    /// <summary>
    /// Checks if job should be deactivated based on execution count limit
    /// Returns true if job should continue, false if it should be deactivated
    /// </summary>
    public bool CheckExecutionLimit()
    {
        // Auto-deactivate if execution limit reached
        if (MaxExecutionCount.HasValue && TotalExecutionCount >= MaxExecutionCount.Value)
        {
            IsActive = false;
            return false; // Job should be deactivated
        }

        return true; // Job can continue
    }

    /// <summary>
    /// Ensures POST requests have a valid payload
    /// If HttpMethod is POST and Payload is null or empty, sets default payload: {}
    /// </summary>
    public void EnsurePostPayload()
    {
        if (HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(Payload))
            {
                Payload = DefaultPostPayload;
            }
        }
    }

    /// <summary>
    /// Gets the payload for HTTP request
    /// Returns Payload if set, or default {} for POST requests, or null for GET requests
    /// </summary>
    public string? GetPayloadForRequest()
    {
        if (HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase))
        {
            return string.IsNullOrWhiteSpace(Payload) ? DefaultPostPayload : Payload;
        }
        return null; // GET requests don't have payload
    }
}

/// <summary>
/// Retry policy configuration
/// </summary>
public class RetryPolicy
{
    /// <summary>
    /// Maximum number of retries
    /// </summary>
    [BsonElement("maxRetries")]
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Retry interval in seconds
    /// </summary>
    [BsonElement("retryIntervalSeconds")]
    public int RetryIntervalSeconds { get; set; } = 60;
}
