using MediatR;
using MngKeeper.Application.Common.DTOs;

namespace MngKeeper.Application.Features.Group.Queries.GetGroup
{
    public class GetGroupQuery : IRequest<GetGroupResponse>
    {
        public string GroupId { get; set; } = string.Empty;
    }

    public class GetGroupResponse
    {
        public GetGroupResponseDto? Group { get; set; }
        public bool IsSuccess { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
