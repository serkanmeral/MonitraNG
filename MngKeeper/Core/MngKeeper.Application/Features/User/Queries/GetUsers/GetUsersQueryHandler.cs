using MediatR;
using MngKeeper.Application.Interfaces;
using MngKeeper.Application.Common.DTOs;
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

        public GetUsersQueryHandler(
            IUserRepository userRepository,
            ILogger<GetUsersQueryHandler> logger,
            IHttpContextAccessor httpContextAccessor)
        {
            _userRepository = userRepository;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
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

                var users = await _userRepository.GetByDomainIdAsync(claims.DomainId);
                var usersList = users.ToList();
                var filteredUsers = new List<MngKeeper.Domain.Entities.User>();
                
                // Apply search filter
                if (!string.IsNullOrEmpty(request.SearchTerm))
                {
                    var searchTerm = request.SearchTerm.ToLower();
                    foreach (var user in usersList)
                    {
                        if (user.Username.ToLower().Contains(searchTerm) ||
                            user.Email.ToLower().Contains(searchTerm) ||
                            user.FirstName.ToLower().Contains(searchTerm) ||
                            user.LastName.ToLower().Contains(searchTerm))
                        {
                            filteredUsers.Add(user);
                        }
                    }
                }
                else
                {
                    filteredUsers = usersList;
                }

                // Apply active filter
                if (request.IsActive.HasValue)
                {
                    var activeFiltered = filteredUsers.Where(u => u.IsActive == request.IsActive.Value);
                    filteredUsers = activeFiltered.ToList();
                }

                var totalCount = filteredUsers.Count;
                var totalPages = (int)Math.Ceiling((double)totalCount / request.PageSize);

                // Apply pagination
                var pagedUsers = filteredUsers
                    .Skip((request.Page - 1) * request.PageSize)
                    .Take(request.PageSize)
                    .ToList();

                var userDtos = pagedUsers.Select(u => new UserDto
                {
                    UserId = u.Id,
                    Username = u.Username,
                    Email = u.Email,
                    FirstName = u.FirstName,
                    LastName = u.LastName,
                    IsActive = u.IsActive,
                    Groups = u.Groups,
                    CreatedAt = u.CreatedAt,
                    UpdatedAt = u.UpdatedAt
                }).ToList();

                return new GetUsersResponse
                {
                    Users = userDtos,
                    TotalCount = totalCount,
                    Page = request.Page,
                    PageSize = request.PageSize,
                    TotalPages = totalPages,
                    IsSuccess = true
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting users");
                return new GetUsersResponse
                {
                    IsSuccess = false,
                    ErrorMessage = $"Failed to get users: {ex.Message}"
                };
            }
        }
    }
}
