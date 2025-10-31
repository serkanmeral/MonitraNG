using MngKeeper.Application.Features.Domain.Commands.CreateDomain;

namespace MngKeeper.Application.Interfaces
{
    public interface IKeycloakService
    {
        Task<RealmInfo> CreateRealmAsync(string realmName, DomainSettingsDto settings);
        Task<UserInfo> CreateUserAsync(string realmName, CreateUserRequest request);
        Task<GroupInfo> CreateGroupAsync(string realmName, CreateGroupRequest request);
        Task<bool> AddUserToGroupAsync(string realmName, string userId, string groupName);
        Task<bool> IsUserInGroupAsync(string realmName, string username, string groupName);
        Task<KeycloakTokenResponse> GetTokenAsync(string realmName, string username, string password);
        Task<KeycloakTokenResponse> RefreshTokenAsync(string realmName, string refreshToken);
        Task<bool> RevokeTokenAsync(string realmName, string refreshToken);
        Task<bool> ValidateTokenAsync(string token);
        Task<bool> DeleteRealmAsync(string realmName);
        Task<bool> DeleteUserAsync(string realmName, string userId);
        Task<bool> DeleteGroupAsync(string realmName, string groupId);
    }

    public class RealmInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class UserInfo
    {
        public string Id { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class GroupInfo
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class CreateUserRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public List<string> Groups { get; set; } = new();
        public List<string> Roles { get; set; } = new();
    }

    public class CreateGroupRequest
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<string> Permissions { get; set; } = new();
    }

    public class KeycloakTokenResponse
    {
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public int ExpiresIn { get; set; }
        public int RefreshExpiresIn { get; set; }
        public string TokenType { get; set; } = "Bearer";
        public string? Error { get; set; }
    }
}
