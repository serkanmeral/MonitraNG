namespace MngDocument.Application.Interfaces;

/// <summary>
/// JWT (gateway-forwarded Bearer) üzerinden çağıran kimliği.
/// </summary>
public interface IRequestContext
{
    string? UserId { get; }
    string? Username { get; }
    string? DomainId { get; }
    string? DomainName { get; }
    IReadOnlyList<string> UserGroups { get; }
    bool IsAdmin { get; }
    string? BearerToken { get; }
}
