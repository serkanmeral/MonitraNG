using MediatR;
using MngKeeper.Application.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;

namespace MngKeeper.Application.Features.User.Commands.DeleteUser
{
    public class DeleteUserCommandHandler : IRequestHandler<DeleteUserCommand, DeleteUserResponse>
    {
        private readonly IUserRepository _userRepository;
        private readonly IDomainRepository _domainRepository;
        private readonly IKeycloakService _keycloakService;
        private readonly ILogger<DeleteUserCommandHandler> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public DeleteUserCommandHandler(
            IUserRepository userRepository,
            IDomainRepository domainRepository,
            IKeycloakService keycloakService,
            ILogger<DeleteUserCommandHandler> logger,
            IHttpContextAccessor httpContextAccessor)
        {
            _userRepository = userRepository;
            _domainRepository = domainRepository;
            _keycloakService = keycloakService;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<DeleteUserResponse> Handle(DeleteUserCommand request, CancellationToken cancellationToken)
        {
            TokenClaims? claims = null;
            try
            {
                _logger.LogInformation("Deleting user: {UserId}", request.UserId);

                // Get domain from token claims
                claims = _httpContextAccessor.HttpContext?.Items["TokenClaims"] as TokenClaims;
                
                if (claims?.DomainId == null)
                {
                    return new DeleteUserResponse
                    {
                        IsSuccess = false,
                        ErrorMessage = "Domain information not found in token."
                    };
                }

                // Get domain to get realm name
                var domain = await _domainRepository.GetByIdAsync(claims.DomainId);
                if (domain == null)
                {
                    return new DeleteUserResponse
                    {
                        IsSuccess = false,
                        ErrorMessage = "Domain not found."
                    };
                }

                // Get existing user
                var existingUser = await _userRepository.GetByIdAsync(request.UserId);
                if (existingUser == null)
                {
                    return new DeleteUserResponse
                    {
                        IsSuccess = false,
                        ErrorMessage = "User not found."
                    };
                }

                // Check if user belongs to the current domain
                if (existingUser.DomainId != claims.DomainId)
                {
                    return new DeleteUserResponse
                    {
                        IsSuccess = false,
                        ErrorMessage = "User does not belong to the current domain."
                    };
                }

                // Check if user is a system admin user (domain admin)
                if (existingUser.Username.EndsWith("_admin"))
                {
                    return new DeleteUserResponse
                    {
                        IsSuccess = false,
                        ErrorMessage = "System admin users cannot be deleted."
                    };
                }

                // Delete user from Keycloak (TODO: Implement Keycloak user deletion)
                // For now, we'll just delete from our database

                // Delete from database
                var deleted = await _userRepository.DeleteAsync(request.UserId);
                if (!deleted)
                {
                    return new DeleteUserResponse
                    {
                        IsSuccess = false,
                        ErrorMessage = "Failed to delete user from database."
                    };
                }

                _logger.LogInformation("User deleted successfully: {UserId} in domain: {DomainId}", request.UserId, claims.DomainId);

                return new DeleteUserResponse
                {
                    IsSuccess = true
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting user: {UserId} in domain: {DomainId}", request.UserId, claims?.DomainId);
                return new DeleteUserResponse
                {
                    IsSuccess = false,
                    ErrorMessage = $"Failed to delete user: {ex.Message}"
                };
            }
        }
    }
}
