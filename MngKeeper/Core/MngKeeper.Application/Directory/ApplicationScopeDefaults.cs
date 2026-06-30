using MngKeeper.Domain.Enums;
using MongoDB.Bson;

namespace MngKeeper.Application.Directory;

/// <summary>
/// MonitraNG uygulama kapsamı (<c>includeInApplication</c>) — sync dışı uygulama alanı.
/// Directory kayıtları varsayılan saklı (opt-in); yerel kayıtlar varsayılan görünür.
/// </summary>
public static class ApplicationScopeDefaults
{
    public const string BsonField = "includeInApplication";

    public static bool DefaultForSource(UserProvisioningSource source) =>
        source != UserProvisioningSource.Directory;

    public static bool ResolveFromDocument(BsonDocument doc)
    {
        if (doc.Contains(BsonField) && !doc[BsonField].IsBsonNull)
            return doc[BsonField].AsBoolean;

        var source = doc.Contains("provisioningSource") && !doc["provisioningSource"].IsBsonNull
            ? (UserProvisioningSource)doc["provisioningSource"].AsInt32
            : UserProvisioningSource.Local;

        return DefaultForSource(source);
    }

    public static bool CanAuthenticate(bool isActive, bool includeInApplication) =>
        isActive && includeInApplication;
}
