using MediatR;
using MngKeeper.Application.Common.DTOs;

namespace MngKeeper.Application.Features.User.Queries.GetUser
{
    public class GetUserQuery : IRequest<GetUserResponse>
    {
        public string UserId { get; set; } = string.Empty;
    }

    public class GetUserResponse
    {
        public UserDto? User { get; set; }
        public bool IsSuccess { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
