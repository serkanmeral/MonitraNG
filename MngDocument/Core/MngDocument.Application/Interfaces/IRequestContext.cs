namespace MngDocument.Application.Interfaces;

/// <summary>
/// JWT (gateway-forwarded Bearer) üzerinden çağıran kimliği.
/// </summary>
public interface IRequestContext
{
    string? UserId { get; }
    string? Username { get; }
    /// <summary>JWT <c>name</c> claim — görünen ad (createPerson vb.).</summary>
    string? DisplayName { get; }
    string? DomainId { get; }
    string? DomainName { get; }
    IReadOnlyList<string> UserGroups { get; }
    bool IsAdmin { get; }
    /// <summary>JWT <c>isManager</c> / <c>is_manager</c> — Keeper manager grubu üyeliği.</summary>
    bool IsManager { get; }
    string? BearerToken { get; }
}
