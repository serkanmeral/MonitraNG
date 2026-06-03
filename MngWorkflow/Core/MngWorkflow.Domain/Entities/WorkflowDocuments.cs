using MongoDB.Bson.Serialization.Attributes;
using MngWorkflow.Domain.Constants;
using MngWorkflow.Domain.Enums;

namespace MngWorkflow.Domain.Entities;

public class WorkflowNodeDefinition
{
    [BsonElement("id")]
    public string Id { get; set; } = string.Empty;

    [BsonElement("type")]
    public string Type { get; set; } = string.Empty;

    [BsonElement("config")]
    public Dictionary<string, object?> Config { get; set; } = new();
}

public class WorkflowEdgeDefinition
{
    [BsonElement("fromNodeId")]
    public string FromNodeId { get; set; } = string.Empty;

    [BsonElement("toNodeId")]
    public string ToNodeId { get; set; } = string.Empty;

    [BsonElement("edgeKey")]
    public string EdgeKey { get; set; } = "default";
}

public class WorkflowTriggerDefinition
{
    [BsonElement("type")]
    public string Type { get; set; } = WorkflowTriggerTypes.Event;

    [BsonElement("config")]
    public Dictionary<string, object?> Config { get; set; } = new();

    [BsonElement("filterExpression")]
    public string? FilterExpression { get; set; }

    [BsonElement("enabled")]
    public bool Enabled { get; set; } = true;
}

[BsonIgnoreExtraElements]
public class WorkflowTriggerProjectionDocument
{
    [BsonId]
    [BsonRepresentation(MongoDB.Bson.BsonType.String)]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [BsonElement("domainId")]
    public string DomainId { get; set; } = string.Empty;

    [BsonElement("domainName")]
    public string DomainName { get; set; } = string.Empty;

    [BsonElement("workflowId")]
    public string WorkflowId { get; set; } = string.Empty;

    [BsonElement("workflowVersionId")]
    public string WorkflowVersionId { get; set; } = string.Empty;

    [BsonElement("eventType")]
    public string EventType { get; set; } = string.Empty;

    [BsonElement("filterExpression")]
    public string? FilterExpression { get; set; }

    [BsonElement("enabled")]
    public bool Enabled { get; set; } = true;

    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

[BsonIgnoreExtraElements]
public class WorkflowDefinitionDocument
{
    [BsonId]
    [BsonRepresentation(MongoDB.Bson.BsonType.String)]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [BsonElement("domainId")]
    public string DomainId { get; set; } = string.Empty;

    [BsonElement("domainName")]
    public string DomainName { get; set; } = string.Empty;

    [BsonElement("key")]
    public string Key { get; set; } = string.Empty;

    [BsonElement("name")]
    public string Name { get; set; } = string.Empty;

    [BsonElement("category")]
    public string? Category { get; set; }

    [BsonElement("currentVersion")]
    public int CurrentVersion { get; set; }

    [BsonElement("currentVersionId")]
    public string? CurrentVersionId { get; set; }

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

[BsonIgnoreExtraElements]
public class WorkflowVersionDocument
{
    [BsonId]
    [BsonRepresentation(MongoDB.Bson.BsonType.String)]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [BsonElement("workflowId")]
    public string WorkflowId { get; set; } = string.Empty;

    [BsonElement("domainId")]
    public string DomainId { get; set; } = string.Empty;

    [BsonElement("domainName")]
    public string DomainName { get; set; } = string.Empty;

    [BsonElement("version")]
    public int Version { get; set; } = 1;

    [BsonElement("status")]
    [BsonRepresentation(MongoDB.Bson.BsonType.String)]
    public WorkflowVersionStatus Status { get; set; } = WorkflowVersionStatus.Published;

    [BsonElement("entryNodeId")]
    public string EntryNodeId { get; set; } = string.Empty;

    [BsonElement("nodes")]
    public List<WorkflowNodeDefinition> Nodes { get; set; } = new();

    [BsonElement("edges")]
    public List<WorkflowEdgeDefinition> Edges { get; set; } = new();

    [BsonElement("triggers")]
    public List<WorkflowTriggerDefinition> Triggers { get; set; } = new();

    [BsonElement("publishedAt")]
    public DateTime? PublishedAt { get; set; }

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

[BsonIgnoreExtraElements]
public class WorkflowInstanceDocument
{
    [BsonId]
    [BsonRepresentation(MongoDB.Bson.BsonType.String)]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [BsonElement("workflowId")]
    public string WorkflowId { get; set; } = string.Empty;

    [BsonElement("workflowVersionId")]
    public string WorkflowVersionId { get; set; } = string.Empty;

    [BsonElement("domainId")]
    public string DomainId { get; set; } = string.Empty;

    [BsonElement("domainName")]
    public string DomainName { get; set; } = string.Empty;

    [BsonElement("status")]
    [BsonRepresentation(MongoDB.Bson.BsonType.String)]
    public WorkflowInstanceStatus Status { get; set; } = WorkflowInstanceStatus.Running;

    [BsonElement("currentNodes")]
    public List<string> CurrentNodes { get; set; } = new();

    [BsonElement("executionContext")]
    public Dictionary<string, object?> ExecutionContext { get; set; } = new();

    [BsonElement("correlationId")]
    public string CorrelationId { get; set; } = Guid.NewGuid().ToString("N");

    [BsonElement("triggerType")]
    public string TriggerType { get; set; } = "manual";

    [BsonElement("triggerData")]
    public Dictionary<string, object?> TriggerData { get; set; } = new();

    [BsonElement("revision")]
    public long Revision { get; set; }

    [BsonElement("startedAt")]
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("finishedAt")]
    public DateTime? FinishedAt { get; set; }
}

[BsonIgnoreExtraElements]
public class NodeExecutionDocument
{
    [BsonId]
    [BsonRepresentation(MongoDB.Bson.BsonType.String)]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [BsonElement("instanceId")]
    public string InstanceId { get; set; } = string.Empty;

    [BsonElement("domainId")]
    public string DomainId { get; set; } = string.Empty;

    [BsonElement("domainName")]
    public string DomainName { get; set; } = string.Empty;

    [BsonElement("nodeId")]
    public string NodeId { get; set; } = string.Empty;

    [BsonElement("attempt")]
    public int Attempt { get; set; } = 1;

    [BsonElement("status")]
    [BsonRepresentation(MongoDB.Bson.BsonType.String)]
    public NodeExecutionStatus Status { get; set; }

    [BsonElement("output")]
    public Dictionary<string, object?> Output { get; set; } = new();

    [BsonElement("errorMessage")]
    public string? ErrorMessage { get; set; }

    [BsonElement("startedAt")]
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("finishedAt")]
    public DateTime? FinishedAt { get; set; }
}
