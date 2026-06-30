using MngKeeper.Application.Common;
using MngKeeper.Application.Interfaces;
using MngKeeper.Domain.Entities;
using MngKeeper.Domain.Enums;

namespace MngKeeper.Application.Directory;

/// <summary>
/// Directory sync'in yazdığı alanlar (USER_SOURCES.md whitelist).
/// Uygulama alanları (photoUrl, photoSource, gender, title, department, phoneNumber) korunur.
/// </summary>
public static class DirectoryUserFieldSets
{
    /// <summary>K2/K3/K4 yalnızca bu alanları günceller.</summary>
    public static readonly IReadOnlySet<string> SyncFromKeycloak = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "username", "email", "firstName", "lastName",
        "groups", "isActive", "keycloakUserId",
    };

    /// <summary>Directory kullanıcı — PUT /api/user ile düzenlenebilir uygulama alanları.</summary>
    public static readonly IReadOnlySet<string> EditableByApplication = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "photoUrl", "gender", "title", "department", "phoneNumber", "includeInApplication",
    };

    public static void ApplyDirectoryFields(
        User target,
        KeycloakRealmUserSnapshot source,
        IReadOnlyList<string> groupNames,
        DateTime syncedAt)
    {
        target.KeycloakUserId = source.Id;
        target.Username = source.Username;
        target.Email = UserEmailHelper.NormalizeForStorage(source.Email);
        target.FirstName = source.FirstName ?? string.Empty;
        target.LastName = source.LastName ?? string.Empty;
        target.IsActive = source.Enabled;
        target.Groups = groupNames.ToList();
        target.ProvisioningSource = UserProvisioningSource.Directory;
        target.DirectorySyncedAt = syncedAt;
        target.UpdatedAt = syncedAt;
        target.UpdatedBy = MngKeeper.Application.Common.Constants.SystemConstants.SystemUser;
    }

    public static User CreateDirectoryUser(
        string domainId,
        KeycloakRealmUserSnapshot source,
        IReadOnlyList<string> groupNames,
        DateTime syncedAt)
    {
        var user = new User
        {
            Id = MongoDB.Bson.ObjectId.GenerateNewId().ToString(),
            DomainId = domainId,
            CreatedAt = syncedAt,
            CreatedBy = MngKeeper.Application.Common.Constants.SystemConstants.SystemUser,
            Gender = Gender.NotSpecified,
            Roles = new List<string>(),
            IncludeInApplication = ApplicationScopeDefaults.DefaultForSource(UserProvisioningSource.Directory),
        };
        ApplyDirectoryFields(user, source, groupNames, syncedAt);
        return user;
    }
}
