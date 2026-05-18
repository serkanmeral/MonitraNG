namespace MngKeeper.Application.Services;

public sealed class TokenCredentialResolutionResult
{
    public bool IsSuccess { get; init; }
    public string DomainName { get; init; } = string.Empty;
    public string Username { get; init; } = string.Empty;
    public string? ErrorCode { get; init; }
    public string? ErrorDescription { get; init; }

    public static TokenCredentialResolutionResult Ok(string domainName, string username) =>
        new()
        {
            IsSuccess = true,
            DomainName = domainName,
            Username = username
        };

    public static TokenCredentialResolutionResult Fail(string errorCode, string errorDescription) =>
        new()
        {
            IsSuccess = false,
            ErrorCode = errorCode,
            ErrorDescription = errorDescription
        };
}
