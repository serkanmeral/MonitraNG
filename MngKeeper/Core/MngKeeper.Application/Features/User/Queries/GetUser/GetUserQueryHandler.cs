using MediatR;
using MngKeeper.Application.Interfaces;
using MngKeeper.Application.Common.DTOs;
using MngKeeper.Application.Common.Mappers;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;

namespace MngKeeper.Application.Features.User.Queries.GetUser
{
    public class GetUserQueryHandler : IRequestHandler<GetUserQuery, GetUserResponse>
    {
        private readonly IUserRepository _userRepository;
        private readonly ILogger<GetUserQueryHandler> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IUserFieldPolicyService _fieldPolicyService;

        public GetUserQueryHandler(
            IUserRepository userRepository,
            ILogger<GetUserQueryHandler> logger,
            IHttpContextAccessor httpContextAccessor,
            IUserFieldPolicyService fieldPolicyService)
        {
            _userRepository = userRepository;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
            _fieldPolicyService = fieldPolicyService;
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

                // Önce Keeper Mongo __dataId; yoksa Keycloak UUID (JWT sub / cht_messages.authorPersonId)
                var user = await _userRepository.GetByIdAsync(request.UserId, claims.DomainId);
                if (user == null)
                    user = await _userRepository.GetByKeycloakUserIdAsync(request.UserId, claims.DomainId);
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

                var userDto = UserDtoMapper.ToDto(user, _fieldPolicyService);

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
