using MediatR;
using MngKeeper.Application.Common.DTOs;

namespace MngKeeper.Application.Features.Group.Queries.GetGroups
{
    public class GetGroupsQuery : IRequest<GetGroupsResponse>
    {
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public string? SearchTerm { get; set; }
        public bool? IsActive { get; set; }
    }

    public class GetGroupsResponse
    {
        public List<GetGroupsResponseDto> Groups { get; set; } = new();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
        public bool IsSuccess { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
