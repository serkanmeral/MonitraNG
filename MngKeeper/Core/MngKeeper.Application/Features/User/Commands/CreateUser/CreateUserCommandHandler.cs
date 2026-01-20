using MediatR;
using MngKeeper.Application.Interfaces;
using MngKeeper.Domain.Entities;
using MngKeeper.Application.Common.Helpers;
using MngKeeper.Application.Common.Exceptions;
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
        private readonly ILicenseService _licenseService;
        private readonly ILogger<CreateUserCommandHandler> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CreateUserCommandHandler(
            IUserRepository userRepository,
            IDomainRepository domainRepository,
            IGroupRepository groupRepository,
            IKeycloakService keycloakService,
            IEventPublisher eventPublisher,
            IDataGatewaySyncService dataGatewaySyncService,
            ILicenseService licenseService,
            ILogger<CreateUserCommandHandler> logger,
            IHttpContextAccessor httpContextAccessor)
        {
            _userRepository = userRepository;
            _domainRepository = domainRepository;
            _groupRepository = groupRepository;
            _keycloakService = keycloakService;
            _eventPublisher = eventPublisher;
            _dataGatewaySyncService = dataGatewaySyncService;
            _licenseService = licenseService;
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

                // Check license - can we create a new user?
                // Only check limit if the new user will be active
                if (request.IsActive)
                {
                    var canCreateUser = await _licenseService.CanCreateUserAsync(domainValue.Name, cancellationToken);
                    if (!canCreateUser)
                    {
                        var activeUserCount = await _licenseService.GetActiveUserCountAsync(domainValue.Name, cancellationToken);
                        var activeLicense = await _licenseService.GetActiveLicenseAsync(domainValue.Name, cancellationToken);
                        var maxUsers = activeLicense?.LicenseFeatures?.MaxUsers ?? 0;
                        
                        _logger.LogWarning(
                            "User creation blocked due to license limit. Domain: {DomainName}, Current: {CurrentCount}, Max: {MaxUsers}",
                            domainValue.Name,
                            activeUserCount,
                            maxUsers);
                        
                        return new CreateUserResponse
                        {
                            IsSuccess = false,
                            ErrorMessage = $"Kullanıcı limiti aşıldı. Maksimum: {maxUsers}, Mevcut: {activeUserCount}"
                        };
                    }
                }

                // Check if user already exists
                if (await _userRepository.ExistsByEmailAsync(request.Email, claims.DomainId))
                {
                    return new CreateUserResponse
                    {
                        IsSuccess = false,
                        ErrorMessage = $"User with email '{request.Email}' already exists."
                    };
                }

                if (await _userRepository.ExistsByUsernameAsync(request.Username, claims.DomainId))
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
                var usersGroup = domainGroups.FirstOrDefault(g => g.Name == MngKeeper.Application.Common.Constants.SystemGroups.Users && g.DomainId == claims.DomainId);
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

                // Convert group IDs to group names for Keycloak CreateUserRequest and User entity
                // (Keycloak CreateUserAsync uses group names for isAdmin check, not IDs)
                // User.Groups field stores group names (consistent with AddUserToGroup/RemoveUserFromGroup behavior)
                var groupNames = new List<string>();
                foreach (var groupId in finalGroupIds)
                {
                    var group = await _groupRepository.GetByIdAsync(groupId, claims.DomainId);
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
                    Title = request.Title,
                    Department = request.Department,
                    Gender = (int)request.Gender,
                    PhoneNumber = request.PhoneNumber,
                    PhotoUrl = request.PhotoUrl,
                    Groups = groupNames  // Use group names, not IDs
                };

                var keycloakUser = await _keycloakService.CreateUserAsync(domainValue.RealmName, keycloakUserRequest);

                // Create user entity (only for sync to domain database, not saved to mngkeeper database)
                // Note: User.Groups field stores group names (not IDs) for consistency with AddUserToGroup/RemoveUserFromGroup
                var user = new MngKeeper.Domain.Entities.User
                {
                    Id = MongoDB.Bson.ObjectId.GenerateNewId().ToString(), // Generate new MongoDB ObjectId
                    KeycloakUserId = keycloakUser.Id, // Store Keycloak UUID for later operations
                    Username = request.Username,
                    Email = request.Email,
                    FirstName = request.FirstName,
                    LastName = request.LastName,
                    Title = request.Title,
                    Department = request.Department,
                    Gender = request.Gender,
                    PhoneNumber = request.PhoneNumber,
                    PhotoUrl = request.PhotoUrl,
                    IsActive = request.IsActive,
                    Groups = groupNames, // Store group names (consistent with AddUserToGroup behavior)
                    DomainId = claims.DomainId,
                    CreatedBy = MngKeeper.Application.Common.Constants.SystemConstants.SystemUser, // TODO: Get from current user context
                    CreatedAt = DateTime.UtcNow
                };

                // Save to domain-specific database (users collection)
                var savedUser = await _userRepository.AddAsync(user);
                _logger.LogInformation("User saved to domain database users collection: UserId={UserId}", savedUser.Id);

                // Invalidate user count cache since a new user was created
                try
                {
                    await _licenseService.InvalidateUserCountCacheAsync(domainValue.Name);
                }
                catch (Exception cacheEx)
                {
                    _logger.LogWarning(cacheEx, "Failed to invalidate user count cache after user creation");
                }

                // Add user to groups in Keycloak (if not already added during user creation)
                // Note: Keycloak CreateUserAsync may already add user to groups, but we ensure it here
                // AddUserToGroupAsync expects groupName, not groupId, so we need to get group name from group ID
                var groupsAdded = new List<string>();
                var groupsFailed = new List<string>();
                
                foreach (var groupId in finalGroupIds)
                {
                    try
                    {
                        // Get group by ID to get the name
                        var group = await _groupRepository.GetByIdAsync(groupId, claims.DomainId);
                        if (group != null && group.DomainId == claims.DomainId)
                        {
                            var success = await _keycloakService.AddUserToGroupAsync(domainValue.RealmName, keycloakUser.Id, group.Name);
                            if (success)
                            {
                                groupsAdded.Add(group.Name);
                                _logger.LogInformation("Added user to group: {GroupName} (ID: {GroupId})", group.Name, groupId);
                            }
                            else
                            {
                                groupsFailed.Add(group.Name);
                                _logger.LogWarning("Failed to add user to group: {GroupName} (ID: {GroupId}) - AddUserToGroupAsync returned false", group.Name, groupId);
                            }
                        }
                        else
                        {
                            _logger.LogWarning("Group not found or does not belong to domain: {GroupId}", groupId);
                            groupsFailed.Add($"GroupId:{groupId}");
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log but don't fail - user might already be in the group
                        _logger.LogWarning(ex, "Exception while adding user to group {GroupId}, user may already be in group", groupId);
                        groupsFailed.Add($"GroupId:{groupId}");
                    }
                }
                
                // Log summary
                if (groupsAdded.Count > 0)
                {
                    _logger.LogInformation("Successfully added user to {Count} group(s): {Groups}", groupsAdded.Count, string.Join(", ", groupsAdded));
                }
                if (groupsFailed.Count > 0)
                {
                    _logger.LogWarning("Failed to add user to {Count} group(s): {Groups}", groupsFailed.Count, string.Join(", ", groupsFailed));
                }

                // Sync to domain database (@users collection for DataGateway) with custom data
                try
                {
                    await _dataGatewaySyncService.SyncUserToDataGatewayAsync(
                        savedUser, 
                        claims.DomainId,
                        request.CustomData);
                    _logger.LogInformation("User synced to domain database @users collection: UserId={UserId}", savedUser.Id);
                }
                catch (Exception syncEx)
                {
                    // Log error but don't fail the user creation
                    _logger.LogError(syncEx, "Failed to sync user to domain database @users collection: UserId={UserId}", savedUser.Id);
                    // Continue - user is created in Keycloak and domain database
                }

                // Publish user created event (notification only)
                var userCreatedEvent = new UserCreatedEvent
                {
                    UserId = savedUser.Id,
                    Username = savedUser.Username,
                    Email = savedUser.Email,
                    Groups = savedUser.Groups
                };
                await EventPublishingHelper.PublishEventSafelyAsync(
                    _eventPublisher,
                    _logger,
                    userCreatedEvent,
                    claims.DomainId,
                    "UserCreatedEvent",
                    savedUser.Id);

                _logger.LogInformation("User created successfully: {Username} in domain: {DomainId}", request.Username, claims.DomainId);

                return new CreateUserResponse
                {
                    UserId = savedUser.Id,
                    Username = savedUser.Username,
                    Email = savedUser.Email,
                    FirstName = savedUser.FirstName,
                    LastName = savedUser.LastName,
                    Title = savedUser.Title,
                    Department = savedUser.Department,
                    Gender = savedUser.Gender,
                    PhoneNumber = savedUser.PhoneNumber,
                    PhotoUrl = savedUser.PhotoUrl,
                    IsActive = savedUser.IsActive,
                    CreatedAt = savedUser.CreatedAt,
                    IsSuccess = true
                };
            }
            catch (Exception ex)
            {
                return ResponseHelper.CreateErrorResponse<CreateUserResponse>(
                    _logger,
                    ex,
                    "CreateUser",
                    request.Username,
                    claims?.DomainId ?? "N/A");
            }
        }
    }
}
