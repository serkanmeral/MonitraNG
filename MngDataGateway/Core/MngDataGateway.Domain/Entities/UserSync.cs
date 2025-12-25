using MongoDB.Bson.Serialization.Attributes;
using MngDataGateway.Domain.Entities.Base;

namespace MngDataGateway.Domain.Entities;

/// <summary>
/// User sync entity - MngKeeper'dan sync edilen user verileri
/// Collection: @users
/// </summary>
[BsonIgnoreExtraElements]
public class UserSync : BaseEntity
{
    /// <summary>
    /// MngKeeper'daki user _id (ObjectId string) - Unique identifier
    /// __dataId ile aynı değer (MngKeeper _id)
    /// </summary>
    [BsonElement("keycloakUserId")]
    public string KeycloakUserId { get; set; } = string.Empty;

    /// <summary>
    /// Username
    /// </summary>
    [BsonElement("username")]
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Email address
    /// </summary>
    [BsonElement("email")]
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// First name
    /// </summary>
    [BsonElement("firstName")]
    public string FirstName { get; set; } = string.Empty;

    /// <summary>
    /// Last name
    /// </summary>
    [BsonElement("lastName")]
    public string LastName { get; set; } = string.Empty;

    /// <summary>
    /// Is user active
    /// </summary>
    [BsonElement("isActive")]
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Domain ID (MngKeeper domain ID)
    /// </summary>
    [BsonElement("domainId")]
    public string DomainId { get; set; } = string.Empty;

    /// <summary>
    /// Group IDs (MngKeeper group ObjectId'leri)
    /// </summary>
    [BsonElement("groups")]
    public List<string> Groups { get; set; } = new();

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

/// <summary>
/// Sync information metadata
/// </summary>
public class SyncInfo
{
    /// <summary>
    /// Last sync timestamp (UTC)
    /// </summary>
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc, Representation = MongoDB.Bson.BsonType.DateTime)]
    [BsonElement("lastSyncedAt")]
    public DateTime LastSyncedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Sync source (e.g., "mngkeeper")
    /// </summary>
    [BsonElement("syncSource")]
    public string SyncSource { get; set; } = "mngkeeper";

    /// <summary>
    /// Sync version (increment on each sync)
    /// </summary>
    [BsonElement("syncVersion")]
    public int SyncVersion { get; set; } = 1;
}

