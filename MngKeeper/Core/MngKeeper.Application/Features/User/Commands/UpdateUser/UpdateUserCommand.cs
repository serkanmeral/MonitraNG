using MediatR;
using MngKeeper.Domain.Enums;

namespace MngKeeper.Application.Features.User.Commands.UpdateUser
{
    public class UpdateUserCommand : IRequest<UpdateUserResponse>
    {
        public string UserId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string? Title { get; set; }
        public string? Department { get; set; }
        public Gender Gender { get; set; } = Gender.NotSpecified;
        public string? PhoneNumber { get; set; }
        public string? PhotoUrl { get; set; }
        public List<string>? GroupIds { get; set; } // Nullable: if null, preserve existing groups; if empty list, clear groups; if has items, update groups
        public bool IsActive { get; set; } = true;
        
        /// <summary>
        /// Custom data dictionary - MngDataGateway MongoDB'ye sync edilecek
        /// Keycloak'a yazılmaz, sadece DataGateway'de tutulur
        /// </summary>
        public Dictionary<string, object>? CustomData { get; set; }
    }

    public class UpdateUserResponse
    {
        public string UserId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string? Title { get; set; }
        public string? Department { get; set; }
        public Gender Gender { get; set; } = Gender.NotSpecified;
        public string? PhoneNumber { get; set; }
        public string? PhotoUrl { get; set; }
        public List<string> GroupIds { get; set; } = new();
        public bool IsActive { get; set; }
        public DateTime UpdatedAt { get; set; }
        public bool IsSuccess { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
