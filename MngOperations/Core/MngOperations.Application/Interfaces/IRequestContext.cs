namespace MngOperations.Application.Interfaces;

/// <summary>
/// Caller identity from JWT (gateway-forwarded Bearer).
/// </summary>
public interface IRequestContext
{
    /// <summary>JWT <c>sub</c> — Keycloak kullanıcı id'si (kimlik doğrulama subject).</summary>
    string? UserId { get; }

    /// <summary>
    /// JWT <c>mng_person_id</c> — Keeper domain DB <c>@users</c> kaydı id'si.
    /// Person picker (assignee/watchers/mention) bu id'yi saklar; bildirim alıcıları bu uzaydadır.
    /// Claim yoksa <see cref="UserId"/>'ye düşer.
    /// </summary>
    string? MngPersonId { get; }

    string? Username { get; }
    string? DomainId { get; }
    string? DomainName { get; }
    IReadOnlyList<string> UserGroups { get; }
    bool IsAdmin { get; }
    bool IsManager { get; }
    string? BearerToken { get; }
}
