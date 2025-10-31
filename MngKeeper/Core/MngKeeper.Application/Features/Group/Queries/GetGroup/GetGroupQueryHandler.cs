using MediatR;
using MngKeeper.Application.Interfaces;
using MngKeeper.Application.Common.DTOs;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;

namespace MngKeeper.Application.Features.Group.Queries.GetGroup
{
    public class GetGroupQueryHandler : IRequestHandler<GetGroupQuery, GetGroupResponse>
    {
        private readonly IGroupRepository _groupRepository;
        private readonly ILogger<GetGroupQueryHandler> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public GetGroupQueryHandler(
            IGroupRepository groupRepository,
            ILogger<GetGroupQueryHandler> logger,
            IHttpContextAccessor httpContextAccessor)
        {
            _groupRepository = groupRepository;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<GetGroupResponse> Handle(GetGroupQuery request, CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Getting group: {GroupId}", request.GroupId);

                // Get domain from token claims
                var claims = _httpContextAccessor.HttpContext?.Items["TokenClaims"] as TokenClaims;
                
                if (claims?.DomainId == null)
                {
                    return new GetGroupResponse
                    {
                        IsSuccess = false,
                        ErrorMessage = "Domain information not found in token."
                    };
                }

                // Get group by ID
                var group = await _groupRepository.GetByIdAsync(request.GroupId);
                if (group == null)
                {
                    return new GetGroupResponse
                    {
                        IsSuccess = false,
                        ErrorMessage = "Group not found."
                    };
                }

                // Check if group belongs to the current domain
                if (group.DomainId != claims.DomainId)
                {
                    return new GetGroupResponse
                    {
                        IsSuccess = false,
                        ErrorMessage = "Group does not belong to the current domain."
                    };
                }

                var groupDto = new GetGroupResponseDto
                {
                    GroupId = group.Id,
                    Name = group.Name,
                    Description = group.Description,
                    Permissions = group.Permissions,
                    IsActive = group.IsActive,
                    CreatedAt = group.CreatedAt,
                    UpdatedAt = group.UpdatedAt,
                    CreatedBy = group.CreatedBy,
                    UpdatedBy = group.UpdatedBy
                };

                return new GetGroupResponse
                {
                    Group = groupDto,
                    IsSuccess = true
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting group: {GroupId}", request.GroupId);
                return new GetGroupResponse
                {
                    IsSuccess = false,
                    ErrorMessage = $"Failed to get group: {ex.Message}"
                };
            }
        }
    }
}
