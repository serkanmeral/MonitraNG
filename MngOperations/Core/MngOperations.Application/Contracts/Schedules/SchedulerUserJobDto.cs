namespace MngOperations.Application.Contracts.Schedules;

/// <summary>
/// MngScheduler User Job API gövdesi (POST/PUT /api/v1/user/jobs).
/// </summary>
public sealed class SchedulerUserJobDto
{
    public string JobId { get; set; } = string.Empty;

    /// <summary>1 = User (MngScheduler JobType enum).</summary>
    public int JobType { get; set; } = 1;

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string CronExpression { get; set; } = string.Empty;
    public string EndpointUrl { get; set; } = string.Empty;
    public string HttpMethod { get; set; } = "POST";
    public string? Payload { get; set; } = "{}";
    public bool IsActive { get; set; } = true;
    public int TimeoutSeconds { get; set; } = 300;
}
