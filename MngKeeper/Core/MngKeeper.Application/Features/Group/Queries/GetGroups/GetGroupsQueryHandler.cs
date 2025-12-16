using MediatR;
using MngKeeper.Application.Interfaces;
using MngKeeper.Application.Common.DTOs;
using MngKeeper.Application.Common.Constants;
using MngKeeper.Application.Common.Exceptions;
using MngKeeper.Application.Common.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using System;

namespace MngKeeper.Application.Features.Group.Queries.GetGroups
{
    public class GetGroupsQueryHandler : IRequestHandler<GetGroupsQuery, GetGroupsResponse>
    {
        private readonly IGroupRepository _groupRepository;
        private readonly IRedisService _redisService;
        private readonly ILogger<GetGroupsQueryHandler> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public GetGroupsQueryHandler(
            IGroupRepository groupRepository,
            IRedisService redisService,
            ILogger<GetGroupsQueryHandler> logger,
            IHttpContextAccessor httpContextAccessor)
        {
            _groupRepository = groupRepository;
            _redisService = redisService;
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

                // Build cache key
                var cacheKey = CacheExtensions.BuildCacheKey(
                    "groups",
                    claims.DomainId,
                    request.Page,
                    request.PageSize,
                    request.SearchTerm,
                    request.IsActive);

                // Get or set from cache
                var response = await _redisService.GetOrSetAsync(
                    cacheKey,
                    async () =>
                    {
                        // Optimized: Database-level filtering and pagination
                        var queryResult = await _groupRepository.GetByDomainIdWithPaginationAsync(
                            claims.DomainId,
                            request.Page,
                            request.PageSize,
                            request.SearchTerm,
                            request.IsActive);

                        var groupDtos = queryResult.Items.Select(g => new GetGroupsResponseDto
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
                            TotalCount = queryResult.TotalCount,
                            Page = queryResult.Page,
                            PageSize = queryResult.PageSize,
                            TotalPages = queryResult.TotalPages,
                            IsSuccess = true
                        };
                    },
                    TimeSpan.FromMinutes(SystemConstants.Cache.GroupsList),
                    _logger);

                return response;
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
