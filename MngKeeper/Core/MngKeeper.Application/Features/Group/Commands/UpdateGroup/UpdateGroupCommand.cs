using MediatR;

namespace MngKeeper.Application.Features.Group.Commands.UpdateGroup
{
    public class UpdateGroupCommand : IRequest<UpdateGroupResponse>
    {
        public string GroupId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<string> Permissions { get; set; } = new();
        public bool IsActive { get; set; } = true;
    }

    public class UpdateGroupResponse
    {
        public string GroupId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<string> Permissions { get; set; } = new();
        public bool IsActive { get; set; }
        public DateTime UpdatedAt { get; set; }
        public bool IsSuccess { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
