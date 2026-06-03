using MongoDB.Bson.Serialization.Attributes;
using MngWorkflow.Domain.Enums;

namespace MngWorkflow.Domain.Entities;

[BsonIgnoreExtraElements]
public class WorkflowApprovalDocument
{
    [BsonId]
    [BsonRepresentation(MongoDB.Bson.BsonType.String)]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [BsonElement("instanceId")]
    public string InstanceId { get; set; } = string.Empty;

    [BsonElement("workflowId")]
    public string WorkflowId { get; set; } = string.Empty;

    [BsonElement("workflowVersionId")]
    public string WorkflowVersionId { get; set; } = string.Empty;

    [BsonElement("domainId")]
    public string DomainId { get; set; } = string.Empty;

    [BsonElement("domainName")]
    public string DomainName { get; set; } = string.Empty;

    [BsonElement("nodeId")]
    public string NodeId { get; set; } = string.Empty;

    [BsonElement("approverTarget")]
    public string ApproverTarget { get; set; } = string.Empty;

    [BsonElement("status")]
    [BsonRepresentation(MongoDB.Bson.BsonType.String)]
    public WorkflowApprovalStatus Status { get; set; } = WorkflowApprovalStatus.Pending;

    [BsonElement("decidedBy")]
    public string? DecidedBy { get; set; }

    [BsonElement("comment")]
    public string? Comment { get; set; }

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("decidedAt")]
    public DateTime? DecidedAt { get; set; }
}

[BsonIgnoreExtraElements]
public class WorkflowSecretDocument
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

    [BsonElement("encryptedValue")]
    public string EncryptedValue { get; set; } = string.Empty;

    [BsonElement("algo")]
    public string Algo { get; set; } = "AES-GCM";

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
