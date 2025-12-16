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
        private readonly IGroupRepository _groupRepository;
        private readonly IKeycloakService _keycloakService;
        private readonly IEventPublisher _eventPublisher;
        private readonly IDataGatewaySyncService _dataGatewaySyncService;
        private readonly ILogger<CreateUserCommandHandler> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CreateUserCommandHandler(
            IUserRepository userRepository,
            IDomainRepository domainRepository,
            IGroupRepository groupRepository,
            IKeycloakService keycloakService,
            IEventPublisher eventPublisher,
            IDataGatewaySyncService dataGatewaySyncService,
            ILogger<CreateUserCommandHandler> logger,
            IHttpContextAccessor httpContextAccessor)
        {
            _userRepository = userRepository;
            _domainRepository = domainRepository;
            _groupRepository = groupRepository;
            _keycloakService = keycloakService;
            _eventPublisher = eventPublisher;
            _dataGatewaySyncService = dataGatewaySyncService;
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
                
                MngKeeper.Domain.Entities.Domain? domain = null;
                
                // Try to get domain by ID first
                if (claims?.DomainId != null)
                {
                    domain = await _domainRepository.GetByIdAsync(claims.DomainId);
                }
                
                // If domain not found by ID, try to find by name
                if (domain is null && !string.IsNullOrEmpty(claims?.DomainName))
                {
                    domain = await _domainRepository.GetByNameAsync(claims.DomainName);
                    // Update claims with the found domain ID
                    if (domain is not null && claims is not null)
                    {
                        claims.DomainId = domain!.Id;
                    }
                }
                
                if (domain is null)
                {
                    return new CreateUserResponse
                    {
                        IsSuccess = false,
                        ErrorMessage = "Domain information not found in token or domain does not exist."
                    };
                }

                // At this point, domain is guaranteed to be non-null
                MngKeeper.Domain.Entities.Domain domainValue = domain!;

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

                // Ensure user is added to "users" group by default
                var finalGroupIds = new List<string>(request.GroupIds ?? new List<string>());
                
                // Find "users" group in the domain
                // Get all groups for the domain and find "users" group
                var domainGroups = await _groupRepository.GetByDomainIdAsync(claims.DomainId);
                var usersGroup = domainGroups.FirstOrDefault(g => g.Name == "users" && g.DomainId == claims.DomainId);
                if (usersGroup != null)
                {
                    // Check if "users" group is not already in the list
                    if (!finalGroupIds.Contains(usersGroup.Id))
                    {
                        finalGroupIds.Add(usersGroup.Id);
                        _logger.LogInformation("Adding user to default 'users' group: {GroupId}", usersGroup.Id);
                    }
                }
                else
                {
                    _logger.LogWarning("Default 'users' group not found in domain: {DomainId}. User will be created without default group.", claims.DomainId);
                }

                // Convert group IDs to group names for Keycloak CreateUserRequest
                // (Keycloak CreateUserAsync uses group names for isAdmin check, not IDs)
                var groupNames = new List<string>();
                foreach (var groupId in finalGroupIds)
                {
                    var group = await _groupRepository.GetByIdAsync(groupId);
                    if (group != null && group.DomainId == claims.DomainId)
                    {
                        groupNames.Add(group.Name);
                    }
                }

                // Create user in Keycloak
                var keycloakUserRequest = new CreateUserRequest
                {
                    Username = request.Username,
                    Email = request.Email,
                    Password = request.Password,
                    FirstName = request.FirstName,
                    LastName = request.LastName,
                    Groups = groupNames  // Use group names, not IDs
                };

                var keycloakUser = await _keycloakService.CreateUserAsync(domainValue.RealmName, keycloakUserRequest);

                // Create user entity
                var user = new MngKeeper.Domain.Entities.User
                {
                    Id = MongoDB.Bson.ObjectId.GenerateNewId().ToString(), // Generate new MongoDB ObjectId
                    KeycloakUserId = keycloakUser.Id, // Store Keycloak UUID for later operations
                    Username = request.Username,
                    Email = request.Email,
                    FirstName = request.FirstName,
                    LastName = request.LastName,
                    IsActive = request.IsActive,
                    Groups = finalGroupIds, // Use finalGroupIds which includes "users" group
                    DomainId = claims.DomainId,
                    CreatedBy = "system", // TODO: Get from current user context
                    CreatedAt = DateTime.UtcNow
                };

                // Save to database
                var savedUser = await _userRepository.AddAsync(user);

                // Add user to groups in Keycloak (if not already added during user creation)
                // Note: Keycloak CreateUserAsync may already add user to groups, but we ensure it here
                // AddUserToGroupAsync expects groupName, not groupId, so we need to get group name from group ID
                foreach (var groupId in finalGroupIds)
                {
                    try
                    {
                        // Get group by ID to get the name
                        var group = await _groupRepository.GetByIdAsync(groupId);
                        if (group != null && group.DomainId == claims.DomainId)
                        {
                            await _keycloakService.AddUserToGroupAsync(domainValue.RealmName, keycloakUser.Id, group.Name);
                            _logger.LogInformation("Added user to group: {GroupName} (ID: {GroupId})", group.Name, groupId);
                        }
                        else
                        {
                            _logger.LogWarning("Group not found or does not belong to domain: {GroupId}", groupId);
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log but don't fail - user might already be in the group
                        _logger.LogWarning(ex, "Failed to add user to group {GroupId}, user may already be in group", groupId);
                    }
                }

                // Sync to DataGateway MongoDB (mng_{domain} database) with custom data
                try
                {
                    await _dataGatewaySyncService.SyncUserToDataGatewayAsync(
                        savedUser, 
                        claims.DomainId,
                        request.CustomData);
                    _logger.LogInformation("User synced to DataGateway: UserId={UserId}", savedUser.Id);
                }
                catch (Exception syncEx)
                {
                    // Log error but don't fail the user creation
                    _logger.LogError(syncEx, "Failed to sync user to DataGateway MongoDB: UserId={UserId}", savedUser.Id);
                    // Continue - user is created in Keycloak and MngKeeper DB
                }

                // Publish user created event (notification only)
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
