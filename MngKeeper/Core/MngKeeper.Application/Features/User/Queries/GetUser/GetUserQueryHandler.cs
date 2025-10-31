using MediatR;
using MngKeeper.Application.Interfaces;
using MngKeeper.Application.Common.DTOs;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;

namespace MngKeeper.Application.Features.User.Queries.GetUser
{
    public class GetUserQueryHandler : IRequestHandler<GetUserQuery, GetUserResponse>
    {
        private readonly IUserRepository _userRepository;
        private readonly ILogger<GetUserQueryHandler> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public GetUserQueryHandler(
            IUserRepository userRepository,
            ILogger<GetUserQueryHandler> logger,
            IHttpContextAccessor httpContextAccessor)
        {
            _userRepository = userRepository;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<GetUserResponse> Handle(GetUserQuery request, CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Getting user: {UserId}", request.UserId);

                // Get domain from token claims
                var claims = _httpContextAccessor.HttpContext?.Items["TokenClaims"] as TokenClaims;
                
                if (claims?.DomainId == null)
                {
                    return new GetUserResponse
                    {
                        IsSuccess = false,
                        ErrorMessage = "Domain information not found in token."
                    };
                }

                // Get user by ID
                var user = await _userRepository.GetByIdAsync(request.UserId);
                if (user == null)
                {
                    return new GetUserResponse
                    {
                        IsSuccess = false,
                        ErrorMessage = "User not found."
                    };
                }

                // Check if user belongs to the current domain
                if (user.DomainId != claims.DomainId)
                {
                    return new GetUserResponse
                    {
                        IsSuccess = false,
                        ErrorMessage = "User does not belong to the current domain."
                    };
                }

                var userDto = new UserDto
                {
                    UserId = user.Id,
                    Username = user.Username,
                    Email = user.Email,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    Groups = user.Groups,
                    IsActive = user.IsActive,
                    CreatedAt = user.CreatedAt,
                    UpdatedAt = user.UpdatedAt,
                    CreatedBy = user.CreatedBy,
                    UpdatedBy = user.UpdatedBy
                };

                return new GetUserResponse
                {
                    User = userDto,
                    IsSuccess = true
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user: {UserId}", request.UserId);
                return new GetUserResponse
                {
                    IsSuccess = false,
                    ErrorMessage = $"Failed to get user: {ex.Message}"
                };
            }
        }
    }
}
