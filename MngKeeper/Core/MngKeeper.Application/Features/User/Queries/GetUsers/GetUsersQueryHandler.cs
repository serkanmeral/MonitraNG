using MediatR;
using MngKeeper.Application.Interfaces;
using MngKeeper.Application.Common.DTOs;
using MngKeeper.Application.Common.Mappers;
using MngKeeper.Application.Common.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using System;

namespace MngKeeper.Application.Features.User.Queries.GetUsers
{
    public class GetUsersQueryHandler : IRequestHandler<GetUsersQuery, GetUsersResponse>
    {
        private readonly IUserRepository _userRepository;
        private readonly ILogger<GetUsersQueryHandler> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IUserFieldPolicyService _fieldPolicyService;

        public GetUsersQueryHandler(
            IUserRepository userRepository,
            ILogger<GetUsersQueryHandler> logger,
            IHttpContextAccessor httpContextAccessor,
            IUserFieldPolicyService fieldPolicyService)
        {
            _userRepository = userRepository;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
            _fieldPolicyService = fieldPolicyService;
        }

        public async Task<GetUsersResponse> Handle(GetUsersQuery request, CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Getting users, Page: {Page}, PageSize: {PageSize}", 
                    request.Page, request.PageSize);

                // Get domain from token claims
                var claims = _httpContextAccessor.HttpContext?.Items["TokenClaims"] as TokenClaims;
                
                if (claims?.DomainId == null)
                {
                    return new GetUsersResponse
                    {
                        IsSuccess = false,
                        ErrorMessage = "Domain information not found in token."
                    };
                }

                // Get users directly from database (no cache)
                var queryResult = await _userRepository.GetByDomainIdWithPaginationAsync(
                    claims.DomainId,
                    request.Page,
                    request.PageSize,
                    request.SearchTerm,
                    request.IsActive,
                    request.SortBy,
                    request.SortOrder);

                var userDtos = queryResult.Items
                    .Select(u => UserDtoMapper.ToDto(u, _fieldPolicyService))
                    .ToList();

                return new GetUsersResponse
                {
                    Users = userDtos,
                    TotalCount = queryResult.TotalCount,
                    Page = queryResult.Page,
                    PageSize = queryResult.PageSize,
                    TotalPages = queryResult.TotalPages,
                    IsSuccess = true
                };
            }
            catch (Exception ex)
            {
                ExceptionHelper.LogException(_logger, ex, "GetUsers", request.Page, request.PageSize);
                return new GetUsersResponse
                {
                    IsSuccess = false,
                    ErrorMessage = ExceptionHelper.GetUserFriendlyMessage(ex)
                };
            }
        }
    }
}
