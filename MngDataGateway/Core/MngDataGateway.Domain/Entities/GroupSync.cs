using MongoDB.Bson.Serialization.Attributes;
using MngDataGateway.Domain.Entities.Base;

namespace MngDataGateway.Domain.Entities;

/// <summary>
/// Group sync entity - MngKeeper'dan sync edilen group verileri
/// Collection: @groups
/// </summary>
[BsonIgnoreExtraElements]
public class GroupSync : BaseEntity
{
    /// <summary>
    /// Group name (unique per domain)
    /// </summary>
    [BsonElement("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Group description
    /// </summary>
    [BsonElement("description")]
    public string? Description { get; set; }

    /// <summary>
    /// Permissions list
    /// </summary>
    [BsonElement("permissions")]
    public List<string> Permissions { get; set; } = new();

    /// <summary>
    /// Domain ID (MngKeeper domain ID)
    /// </summary>
    [BsonElement("domainId")]
    public string DomainId { get; set; } = string.Empty;

    /// <summary>
    /// Sync metadata - MngKeeper'dan sync bilgileri
    /// </summary>
    [BsonElement("__syncInfo")]
    public SyncInfo SyncInfo { get; set; } = new();

    /// <summary>
    /// Soft delete flag
    /// </summary>
    [BsonElement("__isDeleted")]
    public bool IsDeleted { get; set; } = false;
}

