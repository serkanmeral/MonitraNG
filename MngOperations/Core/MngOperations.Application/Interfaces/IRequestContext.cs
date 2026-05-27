namespace MngOperations.Application.Interfaces;

/// <summary>
/// Caller identity from JWT (gateway-forwarded Bearer).
/// </summary>
public interface IRequestContext
{
    string? UserId { get; }
    string? Username { get; }
    string? DomainId { get; }
    string? DomainName { get; }
    IReadOnlyList<string> UserGroups { get; }
    bool IsAdmin { get; }
    bool IsManager { get; }
    string? BearerToken { get; }
}
