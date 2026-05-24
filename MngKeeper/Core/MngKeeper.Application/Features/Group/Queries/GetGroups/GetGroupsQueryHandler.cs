using MediatR;
using MngKeeper.Application.Interfaces;
using MngKeeper.Application.Common.DTOs;
using MngKeeper.Application.Common.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using System;

namespace MngKeeper.Application.Features.Group.Queries.GetGroups
{
    public class GetGroupsQueryHandler : IRequestHandler<GetGroupsQuery, GetGroupsResponse>
    {
        private readonly IGroupRepository _groupRepository;
        private readonly IUserRepository _userRepository;
        private readonly IGroupFieldPolicyService _groupFieldPolicyService;
        private readonly ILogger<GetGroupsQueryHandler> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public GetGroupsQueryHandler(
            IGroupRepository groupRepository,
            IUserRepository userRepository,
            IGroupFieldPolicyService groupFieldPolicyService,
            ILogger<GetGroupsQueryHandler> logger,
            IHttpContextAccessor httpContextAccessor)
        {
            _groupRepository = groupRepository;
            _userRepository = userRepository;
            _groupFieldPolicyService = groupFieldPolicyService;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<GetGroupsResponse> Handle(GetGroupsQuery request, CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Getting groups, Page: {Page}, PageSize: {PageSize}", 
                    request.Page, request.PageSize);

                // Get domain from token claims
                var claims = _httpContextAccessor.HttpContext?.Items["TokenClaims"] as TokenClaims;
                
                if (claims?.DomainId == null)
                {
                    return new GetGroupsResponse
                    {
                        IsSuccess = false,
                        ErrorMessage = "Domain information not found in token."
                    };
                }

                // Get groups directly from database (no cache)
                var queryResult = await _groupRepository.GetByDomainIdWithPaginationAsync(
                    claims.DomainId,
                    request.Page,
                    request.PageSize,
                    request.SearchTerm,
                    request.IsActive);

                // Calculate member count for each group
                var groupDtos = new List<GetGroupsResponseDto>();
                foreach (var g in queryResult.Items)
                {
                    var usersInGroup = await _userRepository.GetByGroupIdAsync(g.Id, claims.DomainId);
                    var memberCount = usersInGroup.Count();
                    
                    groupDtos.Add(new GetGroupsResponseDto
                    {
                        GroupId = g.Id,
                        Name = g.Name,
                        Description = g.Description,
                        Permissions = g.Permissions,
                        IsActive = g.IsActive,
                        MemberCount = memberCount,
                        CreatedAt = g.CreatedAt,
                        UpdatedAt = g.UpdatedAt,
                        ProvisioningSource = g.ProvisioningSource.ToString(),
                        DirectorySyncedAt = g.DirectorySyncedAt,
                        Capabilities = _groupFieldPolicyService.GetCapabilities(g),
                    });
                }

                return new GetGroupsResponse
                {
                    Groups = groupDtos,
                    TotalCount = queryResult.TotalCount,
                    Page = queryResult.Page,
                    PageSize = queryResult.PageSize,
                    TotalPages = queryResult.TotalPages,
                    IsSuccess = true
                };
            }
            catch (Exception ex)
            {
                ExceptionHelper.LogException(_logger, ex, "GetGroups", request.Page, request.PageSize);
                return new GetGroupsResponse
                {
                    IsSuccess = false,
                    ErrorMessage = ExceptionHelper.GetUserFriendlyMessage(ex)
                };
            }
        }
    }
}
