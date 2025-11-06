using MongoDB.Bson.Serialization.Attributes;

namespace MngDataGateway.Domain.Entities.Base;

/// <summary>
/// Base entity class - Tüm entities için ortak metadata pattern
/// </summary>
[BsonIgnoreExtraElements]
public abstract class BaseEntity
{
    /// <summary>
    /// Unique identifier (GUID) - Backend otomatik oluşturur
    /// </summary>
    public string __dataId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Creation metadata - Token'dan alınır, hiç değişmez
    /// </summary>
    public CreateInfo __createInfo { get; set; } = null!;

    /// <summary>
    /// Last update metadata - Her update'te güncellenir
    /// </summary>
    public UpdateInfo? __lastUpdateInfo { get; set; }

    /// <summary>
    /// History - Self logging (MaxHistoryEntries ile sınırlı)
    /// </summary>
    public List<HistoryEntry> __history { get; set; } = new();
}

/// <summary>
/// Creation information
/// </summary>
public class CreateInfo
{
    /// <summary>
    /// Creation timestamp (UTC)
    /// </summary>
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc, Representation = MongoDB.Bson.BsonType.DateTime)]
    public DateTime createdAt { get; set; }

    /// <summary>
    /// Creator user information
    /// </summary>
    public UserInfo userInfo { get; set; } = null!;
}

/// <summary>
/// Last update information
/// </summary>
public class UpdateInfo
{
    /// <summary>
    /// Last update timestamp (UTC)
    /// </summary>
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc, Representation = MongoDB.Bson.BsonType.DateTime)]
    public DateTime updatedAt { get; set; }

    /// <summary>
    /// Last updater user information
    /// </summary>
    public UserInfo userInfo { get; set; } = null!;
}

/// <summary>
/// User information from JWT token
/// </summary>
[BsonIgnoreExtraElements]
public class UserInfo
{
    /// <summary>
    /// User ID (from JWT sub or NameIdentifier claim)
    /// </summary>
    public string uid { get; set; } = string.Empty;

    /// <summary>
    /// Username (from JWT preferred_username claim)
    /// </summary>
    public string userName { get; set; } = string.Empty;

    /// <summary>
    /// Domain name (from JWT domain_name claim)
    /// Database name: mng_{domain}
    /// </summary>
    public string domain { get; set; } = string.Empty;
}

/// <summary>
/// History entry for audit trail
/// </summary>
public class HistoryEntry
{
    /// <summary>
    /// Operation type: insert, update, delete
    /// </summary>
    public string operation { get; set; } = string.Empty;

    /// <summary>
    /// Operation timestamp (UTC)
    /// </summary>
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc, Representation = MongoDB.Bson.BsonType.DateTime)]
    public DateTime timestamp { get; set; }

    /// <summary>
    /// User who performed the operation
    /// </summary>
    public UserInfo userInfo { get; set; } = null!;

    /// <summary>
    /// Changes (only for update operations) - Sadece değişen alanlar
    /// </summary>
    public Dictionary<string, ChangeDetail>? changes { get; set; }
}

/// <summary>
/// Change detail for history (old vs new value)
/// </summary>
public class ChangeDetail
{
    /// <summary>
    /// Old value before update
    /// </summary>
    public object? oldValue { get; set; }

    /// <summary>
    /// New value after update
    /// </summary>
    public object? newValue { get; set; }
}

