using MediatR;

namespace MngKeeper.Application.Features.User.Commands.DeleteUser
{
    public class DeleteUserCommand : IRequest<DeleteUserResponse>
    {
        public string UserId { get; set; } = string.Empty;
    }

    public class DeleteUserResponse
    {
        public bool IsSuccess { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
