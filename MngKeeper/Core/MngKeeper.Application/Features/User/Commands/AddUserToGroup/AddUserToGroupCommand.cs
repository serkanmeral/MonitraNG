using MediatR;

namespace MngKeeper.Application.Features.User.Commands.AddUserToGroup
{
    public class AddUserToGroupCommand : IRequest<AddUserToGroupResponse>
    {
        public string UserId { get; set; } = string.Empty;
        public string DomainId { get; set; } = string.Empty;
        public string GroupId { get; set; } = string.Empty;
    }

    public class AddUserToGroupResponse
    {
        public bool IsSuccess { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
