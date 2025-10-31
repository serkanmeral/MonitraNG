using MediatR;
using MngKeeper.Application.Interfaces;
using MngKeeper.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;

namespace MngKeeper.Application.Features.User.Commands.CreateUser
{
    public class CreateUserCommandHandler : IRequestHandler<CreateUserCommand, CreateUserResponse>
    {
        private readonly IUserRepository _userRepository;
        private readonly IDomainRepository _domainRepository;
        private readonly IKeycloakService _keycloakService;
        private readonly IEventPublisher _eventPublisher;
        private readonly ILogger<CreateUserCommandHandler> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CreateUserCommandHandler(
            IUserRepository userRepository,
            IDomainRepository domainRepository,
            IKeycloakService keycloakService,
            IEventPublisher eventPublisher,
            ILogger<CreateUserCommandHandler> logger,
            IHttpContextAccessor httpContextAccessor)
        {
            _userRepository = userRepository;
            _domainRepository = domainRepository;
            _keycloakService = keycloakService;
            _eventPublisher = eventPublisher;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<CreateUserResponse> Handle(CreateUserCommand request, CancellationToken cancellationToken)
        {
            TokenClaims? claims = null;
            try
            {
                _logger.LogInformation("Creating user: {Username}", request.Username);

                // Get domain from token claims (stored by AdminAuthorizationAttribute)
                claims = _httpContextAccessor.HttpContext?.Items["TokenClaims"] as TokenClaims;
                
                if (claims?.DomainId == null)
                {
                    return new CreateUserResponse
                    {
                        IsSuccess = false,
                        ErrorMessage = "Domain information not found in token."
                    };
                }

                // Get domain to get realm name
                var domain = await _domainRepository.GetByIdAsync(claims.DomainId);
                if (domain == null)
                {
                    return new CreateUserResponse
                    {
                        IsSuccess = false,
                        ErrorMessage = "Domain not found."
                    };
                }

                // Check if user already exists
                if (await _userRepository.ExistsByEmailAsync(request.Email))
                {
                    return new CreateUserResponse
                    {
                        IsSuccess = false,
                        ErrorMessage = $"User with email '{request.Email}' already exists."
                    };
                }

                if (await _userRepository.ExistsByUsernameAsync(request.Username))
                {
                    return new CreateUserResponse
                    {
                        IsSuccess = false,
                        ErrorMessage = $"User with username '{request.Username}' already exists."
                    };
                }

                // Create user in Keycloak
                var keycloakUserRequest = new CreateUserRequest
                {
                    Username = request.Username,
                    Email = request.Email,
                    Password = request.Password,
                    FirstName = request.FirstName,
                    LastName = request.LastName,
                    Groups = request.GroupIds
                };

                var keycloakUser = await _keycloakService.CreateUserAsync(domain.RealmName, keycloakUserRequest);

                // Create user entity
                var user = new MngKeeper.Domain.Entities.User
                {
                    Id = MongoDB.Bson.ObjectId.GenerateNewId().ToString(), // Generate new MongoDB ObjectId
                    Username = request.Username,
                    Email = request.Email,
                    FirstName = request.FirstName,
                    LastName = request.LastName,
                    IsActive = request.IsActive,
                    Groups = request.GroupIds,
                    DomainId = claims.DomainId,
                    CreatedBy = "system", // TODO: Get from current user context
                    CreatedAt = DateTime.UtcNow
                };

                // Save to database
                var savedUser = await _userRepository.AddAsync(user);

                // Add user to groups in Keycloak
                foreach (var groupId in request.GroupIds)
                {
                    await _keycloakService.AddUserToGroupAsync(domain.RealmName, keycloakUser.Id, groupId);
                }

                // Publish user created event
                var userCreatedEvent = new UserCreatedEvent
                {
                    UserId = savedUser.Id,
                    Username = savedUser.Username,
                    Email = savedUser.Email,
                    Groups = savedUser.Groups
                };
                await _eventPublisher.PublishAsync(userCreatedEvent, claims.DomainId);

                _logger.LogInformation("User created successfully: {Username} in domain: {DomainId}", request.Username, claims.DomainId);

                return new CreateUserResponse
                {
                    UserId = savedUser.Id,
                    Username = savedUser.Username,
                    Email = savedUser.Email,
                    FirstName = savedUser.FirstName,
                    LastName = savedUser.LastName,
                    IsActive = savedUser.IsActive,
                    CreatedAt = savedUser.CreatedAt,
                    IsSuccess = true
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating user: {Username} in domain: {DomainId}", request.Username, claims?.DomainId);
                return new CreateUserResponse
                {
                    IsSuccess = false,
                    ErrorMessage = $"Failed to create user: {ex.Message}"
                };
            }
        }
    }
}
