using MediatR;
using MngKeeper.Application.Interfaces;
using MngKeeper.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;

namespace MngKeeper.Application.Features.User.Commands.UpdateUser
{
    public class UpdateUserCommandHandler : IRequestHandler<UpdateUserCommand, UpdateUserResponse>
    {
        private readonly IUserRepository _userRepository;
        private readonly IDomainRepository _domainRepository;
        private readonly IKeycloakService _keycloakService;
        private readonly ILogger<UpdateUserCommandHandler> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public UpdateUserCommandHandler(
            IUserRepository userRepository,
            IDomainRepository domainRepository,
            IKeycloakService keycloakService,
            ILogger<UpdateUserCommandHandler> logger,
            IHttpContextAccessor httpContextAccessor)
        {
            _userRepository = userRepository;
            _domainRepository = domainRepository;
            _keycloakService = keycloakService;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<UpdateUserResponse> Handle(UpdateUserCommand request, CancellationToken cancellationToken)
        {
            TokenClaims? claims = null;
            try
            {
                _logger.LogInformation("Updating user: {UserId}", request.UserId);

                // Get domain from token claims
                claims = _httpContextAccessor.HttpContext?.Items["TokenClaims"] as TokenClaims;
                
                if (claims?.DomainId == null)
                {
                    return new UpdateUserResponse
                    {
                        IsSuccess = false,
                        ErrorMessage = "Domain information not found in token."
                    };
                }

                // Get domain to get realm name
                var domain = await _domainRepository.GetByIdAsync(claims.DomainId);
                if (domain == null)
                {
                    return new UpdateUserResponse
                    {
                        IsSuccess = false,
                        ErrorMessage = "Domain not found."
                    };
                }

                // Get existing user
                var existingUser = await _userRepository.GetByIdAsync(request.UserId);
                if (existingUser == null)
                {
                    return new UpdateUserResponse
                    {
                        IsSuccess = false,
                        ErrorMessage = "User not found."
                    };
                }

                // Check if user belongs to the current domain
                if (existingUser.DomainId != claims.DomainId)
                {
                    return new UpdateUserResponse
                    {
                        IsSuccess = false,
                        ErrorMessage = "User does not belong to the current domain."
                    };
                }

                // Check if new email conflicts with existing user (excluding current user)
                if (request.Email != existingUser.Email && await _userRepository.ExistsByEmailAsync(request.Email))
                {
                    return new UpdateUserResponse
                    {
                        IsSuccess = false,
                        ErrorMessage = $"User with email '{request.Email}' already exists."
                    };
                }

                // Check if new username conflicts with existing user (excluding current user)
                if (request.Username != existingUser.Username && await _userRepository.ExistsByUsernameAsync(request.Username))
                {
                    return new UpdateUserResponse
                    {
                        IsSuccess = false,
                        ErrorMessage = $"User with username '{request.Username}' already exists."
                    };
                }

                // Update user in Keycloak (TODO: Implement Keycloak user update)
                // For now, we'll just update in our database

                // Update user entity
                existingUser.Username = request.Username;
                existingUser.Email = request.Email;
                existingUser.FirstName = request.FirstName;
                existingUser.LastName = request.LastName;
                existingUser.Groups = request.GroupIds;
                existingUser.IsActive = request.IsActive;
                existingUser.UpdatedBy = "system"; // TODO: Get from current user context
                existingUser.UpdatedAt = DateTime.UtcNow;

                // Save to database
                var updatedUser = await _userRepository.UpdateAsync(existingUser);

                _logger.LogInformation("User updated successfully: {UserId} in domain: {DomainId}", request.UserId, claims.DomainId);

                return new UpdateUserResponse
                {
                    UserId = updatedUser.Id,
                    Username = updatedUser.Username,
                    Email = updatedUser.Email,
                    FirstName = updatedUser.FirstName,
                    LastName = updatedUser.LastName,
                    GroupIds = updatedUser.Groups,
                    IsActive = updatedUser.IsActive,
                    UpdatedAt = updatedUser.UpdatedAt ?? DateTime.UtcNow,
                    IsSuccess = true
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating user: {UserId} in domain: {DomainId}", request.UserId, claims?.DomainId);
                return new UpdateUserResponse
                {
                    IsSuccess = false,
                    ErrorMessage = $"Failed to update user: {ex.Message}"
                };
            }
        }
    }
}
