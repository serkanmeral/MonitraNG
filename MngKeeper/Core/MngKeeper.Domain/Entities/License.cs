using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace MngKeeper.Domain.Entities
{
    /// <summary>
    /// License information stored in Domain entity
    /// </summary>
    public class LicenseInfo
    {
        [BsonElement("hasRealLicense")]
        public bool HasRealLicense { get; set; } = false;

        [BsonElement("realLicenseExpiresAt")]
        public DateTime? RealLicenseExpiresAt { get; set; }

        [BsonElement("trialLicenseExpiresAt")]
        public DateTime? TrialLicenseExpiresAt { get; set; }

        [BsonElement("activeLicenseType")]
        [BsonRepresentation(BsonType.String)]
        public LicenseType ActiveLicenseType { get; set; } = LicenseType.Trial;

        [BsonElement("lastLicenseCheck")]
        public DateTime? LastLicenseCheck { get; set; }

        [BsonElement("currentUserCount")]
        public int CurrentUserCount { get; set; } = 0;

        [BsonElement("lastUserCountUpdate")]
        public DateTime? LastUserCountUpdate { get; set; }
    }

    /// <summary>
    /// License type enumeration
    /// </summary>
    public enum LicenseType
    {
        Trial,
        Real
    }

    /// <summary>
    /// License operation types for validation
    /// </summary>
    public enum LicenseOperation
    {
        TokenGeneration,
        CrudOperation,
        GetOperation
    }
}
