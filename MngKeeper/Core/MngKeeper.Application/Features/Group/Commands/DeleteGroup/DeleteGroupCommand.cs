using MediatR;

namespace MngKeeper.Application.Features.Group.Commands.DeleteGroup
{
    public class DeleteGroupCommand : IRequest<DeleteGroupResponse>
    {
        public string GroupId { get; set; } = string.Empty;
    }

    public class DeleteGroupResponse
    {
        public bool IsSuccess { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
