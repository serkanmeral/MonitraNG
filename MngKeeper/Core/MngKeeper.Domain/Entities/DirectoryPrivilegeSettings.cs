using MongoDB.Bson.Serialization.Attributes;

namespace MngKeeper.Domain.Entities;

/// <summary>
/// Domain bazlı admin/manager grup alias'ları (LDAP/KC grup adları rename edilmez).
/// Sistem varsayılanları (admins, managers) kodda her zaman birleştirilir.
/// </summary>
[BsonIgnoreExtraElements]
public class DirectoryPrivilegeSettings
{
    [BsonElement("adminGroupNames")]
    public List<string> AdminGroupNames { get; set; } = new();

    [BsonElement("managerGroupNames")]
    public List<string> ManagerGroupNames { get; set; } = new();
}
