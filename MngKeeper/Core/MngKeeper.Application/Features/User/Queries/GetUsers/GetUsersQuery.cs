using MediatR;
using MngKeeper.Application.Common.DTOs;
using MngKeeper.Domain.Enums;

namespace MngKeeper.Application.Features.User.Queries.GetUsers
{
    public class GetUsersQuery : IRequest<GetUsersResponse>
    {
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public string? SearchTerm { get; set; }
        public bool? IsActive { get; set; }
        public bool? IncludeInApplication { get; set; }
        public string? SortBy { get; set; }
        public string? SortOrder { get; set; } // "asc" or "desc"
        public UserProvisioningSource? ProvisioningSource { get; set; }
        public string? GroupId { get; set; }
        public string? GroupIds { get; set; }
    }

    public class GetUsersResponse
    {
        public List<UserDto> Users { get; set; } = new();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
        public bool IsSuccess { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
