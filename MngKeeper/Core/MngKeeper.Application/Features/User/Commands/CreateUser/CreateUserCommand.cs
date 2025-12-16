using MediatR;

namespace MngKeeper.Application.Features.User.Commands.CreateUser
{
    public class CreateUserCommand : IRequest<CreateUserResponse>
    {
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public List<string> GroupIds { get; set; } = new();
        public bool IsActive { get; set; } = true;
        
        /// <summary>
        /// Custom data dictionary - MngDataGateway MongoDB'ye sync edilecek
        /// Keycloak'a yazılmaz, sadece DataGateway'de tutulur
        /// </summary>
        public Dictionary<string, object>? CustomData { get; set; }
    }

    public class CreateUserResponse
    {
        public string UserId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsSuccess { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
