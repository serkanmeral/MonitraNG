using MediatR;
using MngKeeper.Application.Interfaces;
using MngKeeper.Application.Common.DTOs;
using MngKeeper.Application.Common.Helpers;
using MngKeeper.Application.Common.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;

namespace MngKeeper.Application.Features.Group.Queries.GetGroup
{
    public class GetGroupQueryHandler : IRequestHandler<GetGroupQuery, GetGroupResponse>
    {
        private readonly IGroupRepository _groupRepository;
        private readonly IUserRepository _userRepository;
        private readonly IGroupFieldPolicyService _groupFieldPolicyService;
        private readonly ILogger<GetGroupQueryHandler> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public GetGroupQueryHandler(
            IGroupRepository groupRepository,
            IUserRepository userRepository,
            IGroupFieldPolicyService groupFieldPolicyService,
            ILogger<GetGroupQueryHandler> logger,
            IHttpContextAccessor httpContextAccessor)
        {
            _groupRepository = groupRepository;
            _userRepository = userRepository;
            _groupFieldPolicyService = groupFieldPolicyService;
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
                var group = await _groupRepository.GetByIdAsync(request.GroupId, claims.DomainId);
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

                // Calculate member count (users with this group name in their Groups list)
                var usersInGroup = await _userRepository.GetByGroupIdAsync(group.Id, claims.DomainId);
                var memberCount = usersInGroup.Count();

                var groupDto = new GetGroupResponseDto
                {
                    GroupId = group.Id,
                    Name = group.Name,
                    Description = group.Description,
                    Permissions = group.Permissions,
                    IsActive = group.IsActive,
                    IncludeInApplication = group.IncludeInApplication,
                    MemberCount = memberCount,
                    CreatedAt = group.CreatedAt,
                    UpdatedAt = group.UpdatedAt,
                    CreatedBy = group.CreatedBy,
                    UpdatedBy = group.UpdatedBy,
                    ProvisioningSource = group.ProvisioningSource.ToString(),
                    DirectorySyncedAt = group.DirectorySyncedAt,
                    Capabilities = _groupFieldPolicyService.GetCapabilities(group),
                };

                return new GetGroupResponse
                {
                    Group = groupDto,
                    IsSuccess = true
                };
            }
            catch (Exception ex)
            {
                return ResponseHelper.CreateErrorResponse<GetGroupResponse>(
                    _logger,
                    ex,
                    "GetGroup",
                    request.GroupId);
            }
        }
    }
}
