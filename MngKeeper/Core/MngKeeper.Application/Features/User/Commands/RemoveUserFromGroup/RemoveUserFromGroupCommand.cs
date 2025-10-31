using MediatR;

namespace MngKeeper.Application.Features.User.Commands.RemoveUserFromGroup
{
    public class RemoveUserFromGroupCommand : IRequest<RemoveUserFromGroupResponse>
    {
        public string UserId { get; set; } = string.Empty;
        public string DomainId { get; set; } = string.Empty;
        public string GroupId { get; set; } = string.Empty;
    }

    public class RemoveUserFromGroupResponse
    {
        public bool IsSuccess { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
