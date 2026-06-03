namespace MngWorkflow.Application.Contracts;

public sealed class WorkflowSchedulerUserJobDto
{
    public string JobId { get; set; } = string.Empty;
    public int JobType { get; set; } = 1;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string CronExpression { get; set; } = string.Empty;
    public string EndpointUrl { get; set; } = string.Empty;
    public string HttpMethod { get; set; } = "POST";
    public string? Payload { get; set; } = "{}";
    public bool IsActive { get; set; } = true;
    public int TimeoutSeconds { get; set; } = 300;
    public int? MaxExecutionCount { get; set; }
}

public sealed class WorkflowDelayResumeRequest
{
    public string InstanceId { get; set; } = string.Empty;
    public string NodeId { get; set; } = string.Empty;
    public string? DomainName { get; set; }
    public string? DomainId { get; set; }
    public string EdgeKey { get; set; } = "default";
}

public sealed class WorkflowScheduleRunRequest
{
    public string WorkflowId { get; set; } = string.Empty;
    public string? WorkflowVersionId { get; set; }
    public string? DomainName { get; set; }
    public string? DomainId { get; set; }
}
