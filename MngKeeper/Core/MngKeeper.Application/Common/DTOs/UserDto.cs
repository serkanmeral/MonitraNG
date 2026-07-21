using MngKeeper.Domain.Enums;

namespace MngKeeper.Application.Common.DTOs
{
    public class UserDto
    {
        public string UserId { get; set; } = string.Empty;
        /// <summary>Domain id — Telegram bind deep-link and UI context.</summary>
        public string DomainId { get; set; } = string.Empty;
        /// <summary>Keycloak kullanıcı UUID (JWT <c>sub</c>); sohbet <c>authorPersonId</c> eşlemesi için.</summary>
        public string? KeycloakUserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string? Title { get; set; }
        public string? Department { get; set; }
        public Gender Gender { get; set; } = Gender.NotSpecified;
        public string? PhoneNumber { get; set; }
        public string? TelegramUsername { get; set; }
        public string? TelegramChatId { get; set; }
        public DateTime? TelegramLinkedAt { get; set; }
        public string? PhotoUrl { get; set; }
        public string PhotoSource { get; set; } = "None";
        public bool IsActive { get; set; }
        public bool IncludeInApplication { get; set; } = true;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public string? UpdatedBy { get; set; }
        public List<string> Groups { get; set; } = new();
        public List<string> Permissions { get; set; } = new();

        /// <summary>Local veya Directory — K5 kullanıcı kaynağı.</summary>
        public string ProvisioningSource { get; set; } = "Local";

        public DateTime? DirectorySyncedAt { get; set; }

        public UserCapabilitiesDto Capabilities { get; set; } = new();

        public Dictionary<string, UserFieldPolicyItemDto> FieldPolicies { get; set; } = new();
    }
}
