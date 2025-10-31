using MediatR;

namespace MngKeeper.Application.Features.User.Commands.UpdateUser
{
    public class UpdateUserCommand : IRequest<UpdateUserResponse>
    {
        public string UserId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public List<string> GroupIds { get; set; } = new();
        public bool IsActive { get; set; } = true;
    }

    public class UpdateUserResponse
    {
        public string UserId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public List<string> GroupIds { get; set; } = new();
        public bool IsActive { get; set; }
        public DateTime UpdatedAt { get; set; }
        public bool IsSuccess { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
