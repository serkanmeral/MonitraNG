namespace MngHub.Application.Services;

/// <summary>
/// JWT validation service interface
/// </summary>
public interface IJwtValidator
{
    Task<Dictionary<string, string>> ValidateAsync(string token);
    Task<bool> IsValidAsync(string token);
}

