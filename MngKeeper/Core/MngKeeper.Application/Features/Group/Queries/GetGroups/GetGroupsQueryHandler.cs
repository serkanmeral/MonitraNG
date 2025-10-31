using MediatR;
using MngKeeper.Application.Interfaces;
using MngKeeper.Application.Common.DTOs;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using System;

namespace MngKeeper.Application.Features.Group.Queries.GetGroups
{
    public class GetGroupsQueryHandler : IRequestHandler<GetGroupsQuery, GetGroupsResponse>
    {
        private readonly IGroupRepository _groupRepository;
        private readonly ILogger<GetGroupsQueryHandler> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public GetGroupsQueryHandler(
            IGroupRepository groupRepository,
            ILogger<GetGroupsQueryHandler> logger,
            IHttpContextAccessor httpContextAccessor)
        {
            _groupRepository = groupRepository;
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

                var groups = await _groupRepository.GetByDomainIdAsync(claims.DomainId);
                var groupsList = groups.ToList();
                var filteredGroups = new List<MngKeeper.Domain.Entities.Group>();
                
                // Apply search filter
                if (!string.IsNullOrEmpty(request.SearchTerm))
                {
                    var searchTerm = request.SearchTerm.ToLower();
                    foreach (var group in groupsList)
                    {
                        if (group.Name.ToLower().Contains(searchTerm) ||
                            group.Description.ToLower().Contains(searchTerm))
                        {
                            filteredGroups.Add(group);
                        }
                    }
                }
                else
                {
                    filteredGroups = groupsList;
                }

                // Apply active filter
                if (request.IsActive.HasValue)
                {
                    var activeFiltered = filteredGroups.Where(g => g.IsActive == request.IsActive.Value);
                    filteredGroups = activeFiltered.ToList();
                }

                var totalCount = filteredGroups.Count;
                var totalPages = (int)Math.Ceiling((double)totalCount / request.PageSize);

                // Apply pagination
                var pagedGroups = filteredGroups
                    .Skip((request.Page - 1) * request.PageSize)
                    .Take(request.PageSize)
                    .ToList();

                var groupDtos = pagedGroups.Select(g => new GetGroupsResponseDto
                {
                    GroupId = g.Id,
                    Name = g.Name,
                    Description = g.Description,
                    Permissions = g.Permissions,
                    IsActive = g.IsActive,
                    CreatedAt = g.CreatedAt,
                    UpdatedAt = g.UpdatedAt
                }).ToList();

                return new GetGroupsResponse
                {
                    Groups = groupDtos,
                    TotalCount = totalCount,
                    Page = request.Page,
                    PageSize = request.PageSize,
                    TotalPages = totalPages,
                    IsSuccess = true
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting groups");
                return new GetGroupsResponse
                {
                    IsSuccess = false,
                    ErrorMessage = $"Failed to get groups: {ex.Message}"
                };
            }
        }
    }
}
