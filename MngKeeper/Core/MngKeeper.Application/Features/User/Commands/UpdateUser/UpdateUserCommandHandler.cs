using MediatR;
using MngKeeper.Application.Common;
using MngKeeper.Application.Common.Mappers;
using MngKeeper.Application.Directory;
using MngKeeper.Application.Interfaces;
using MngKeeper.Domain.Entities;
using MngKeeper.Application.Common.Helpers;
using MngKeeper.Application.Common.Exceptions;
using MngKeeper.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace MngKeeper.Application.Features.User.Commands.UpdateUser
{
    public class UpdateUserCommandHandler : IRequestHandler<UpdateUserCommand, UpdateUserResponse>
    {
        private readonly IUserRepository _userRepository;
        private readonly IGroupRepository _groupRepository;
        private readonly IDomainRepository _domainRepository;
        private readonly IKeycloakService _keycloakService;
        private readonly IDataGatewaySyncService _dataGatewaySyncService;
        private readonly IEventPublisher _eventPublisher;
        private readonly ILicenseService _licenseService;
        private readonly IDirectoryCache _directoryCache;
        private readonly ILogger<UpdateUserCommandHandler> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public UpdateUserCommandHandler(
            IUserRepository userRepository,
            IGroupRepository groupRepository,
            IDomainRepository domainRepository,
            IKeycloakService keycloakService,
            IDataGatewaySyncService dataGatewaySyncService,
            IEventPublisher eventPublisher,
            ILicenseService licenseService,
            IDirectoryCache directoryCache,
            ILogger<UpdateUserCommandHandler> logger,
            IHttpContextAccessor httpContextAccessor)
        {
            _userRepository = userRepository;
            _groupRepository = groupRepository;
            _domainRepository = domainRepository;
            _keycloakService = keycloakService;
            _dataGatewaySyncService = dataGatewaySyncService;
            _eventPublisher = eventPublisher;
            _licenseService = licenseService;
            _directoryCache = directoryCache;
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
                var existingUser = await _userRepository.GetByIdAsync(request.UserId, claims.DomainId);
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

                var rejectedFields = DirectoryUserUpdateValidator.GetRejectedFields(request, existingUser);
                if (rejectedFields.Count > 0)
                {
                    return new UpdateUserResponse
                    {
                        IsSuccess = false,
                        ErrorMessage =
                            $"Kurumsal hesap alanları uygulama üzerinden güncellenemez. ({DirectoryUserUpdateValidator.ErrorCode}: {string.Join(", ", rejectedFields)})"
                    };
                }

                var isDirectoryUser = existingUser.ProvisioningSource == UserProvisioningSource.Directory;

                if (isDirectoryUser)
                {
                    return await UpdateDirectoryUserAsync(request, existingUser, claims);
                }

                var normalizedEmail = UserEmailHelper.NormalizeForStorage(request.Email);

                if (normalizedEmail != existingUser.Email
                    && UserEmailHelper.HasValue(normalizedEmail)
                    && await _userRepository.ExistsByEmailAsync(normalizedEmail, claims.DomainId))
                {
                    return new UpdateUserResponse
                    {
                        IsSuccess = false,
                        ErrorMessage = $"User with email '{normalizedEmail}' already exists."
                    };
                }

                // Check if new username conflicts with existing user (excluding current user)
                if (request.Username != existingUser.Username && await _userRepository.ExistsByUsernameAsync(request.Username, claims.DomainId))
                {
                    return new UpdateUserResponse
                    {
                        IsSuccess = false,
                        ErrorMessage = $"User with username '{request.Username}' already exists."
                    };
                }

                // Update user in Keycloak
                try
                {
                    var keycloakUpdateRequest = new MngKeeper.Application.Interfaces.UpdateUserRequest
                    {
                        Username = request.Username,
                        Email = normalizedEmail ?? string.Empty,
                        FirstName = request.FirstName,
                        LastName = request.LastName,
                        Title = request.Title,
                        Department = request.Department,
                        Gender = (int)request.Gender,
                        PhoneNumber = request.PhoneNumber,
                        PhotoUrl = request.PhotoUrl
                    };

                    var keycloakUpdated = await _keycloakService.UpdateUserAsync(
                        domain.RealmName, 
                        existingUser.KeycloakUserId ?? existingUser.Id, 
                        keycloakUpdateRequest);

                    if (!keycloakUpdated)
                    {
                        _logger.LogWarning("Failed to update user in Keycloak: {UserId} in realm {RealmName}", 
                            existingUser.Id, domain.RealmName);
                        // Continue with database update even if Keycloak update fails
                    }
                    else
                    {
                        _logger.LogInformation("User updated in Keycloak: {UserId} in realm {RealmName}", 
                            existingUser.Id, domain.RealmName);
                    }
                }
                catch (Exception keycloakEx)
                {
                    _logger.LogError(keycloakEx, "Error updating user in Keycloak: {UserId} in realm {RealmName}", 
                        existingUser.Id, domain.RealmName);
                    // Continue with database update even if Keycloak update fails
                }

                // Update user entity
                existingUser.Username = request.Username;
                existingUser.Email = normalizedEmail;
                existingUser.FirstName = request.FirstName;
                existingUser.LastName = request.LastName;
                existingUser.Title = request.Title;
                existingUser.Department = request.Department;
                existingUser.Gender = request.Gender;
                existingUser.PhoneNumber = request.PhoneNumber;
                UserPhotoProfileHelper.ApplyPhotoUrlFromRequest(existingUser, request.PhotoUrl);
                
                // Convert group IDs to group names (User.Groups stores group names, not IDs)
                // Only update groups if GroupIds is explicitly provided (not null)
                // If GroupIds is null, keep existing groups unchanged
                // If GroupIds is empty list, clear groups (explicit intent to remove all groups)
                if (request.GroupIds != null)
                {
                    if (request.GroupIds.Any())
                    {
                        var directoryGroupIds = await DirectoryGroupMembershipValidator.GetDirectoryGroupIdsAsync(
                            _groupRepository, claims.DomainId, request.GroupIds, cancellationToken);
                        if (directoryGroupIds.Count > 0)
                        {
                            return new UpdateUserResponse
                            {
                                IsSuccess = false,
                                ErrorMessage =
                                    $"Kurumsal gruplara üyelik uygulama üzerinden atanamaz. ({DirectoryGroupGuard.MembershipErrorCode}: {string.Join(", ", directoryGroupIds)})"
                            };
                        }

                        var groupNames = new List<string>();
                        foreach (var groupId in request.GroupIds)
                        {
                            var group = await _groupRepository.GetByIdAsync(groupId, claims.DomainId);
                            if (group != null && group.DomainId == claims.DomainId)
                            {
                                groupNames.Add(group.Name);
                            }
                        }
                        existingUser.Groups = groupNames;
                    }
                    else
                    {
                        // Empty list means explicitly remove all groups
                        existingUser.Groups = new List<string>();
                    }
                }
                // If GroupIds is null, keep existing groups unchanged (don't modify existingUser.Groups)
                
                var wasActive = existingUser.IsActive;
                
                // Check license limit if user is being activated (was inactive, now active)
                if (!wasActive && request.IsActive)
                {
                    var activeLicense = await _licenseService.GetActiveLicenseAsync(domain.Name);
                    if (activeLicense?.LicenseFeatures != null && activeLicense.LicenseFeatures.MaxUsers > 0)
                    {
                        var currentActiveCount = await _licenseService.GetActiveUserCountAsync(domain.Name);
                        if (currentActiveCount >= activeLicense.LicenseFeatures.MaxUsers)
                        {
                            _logger.LogWarning(
                                "User activation blocked due to license limit. Domain: {DomainName}, Current: {CurrentCount}, Max: {MaxUsers}",
                                domain.Name,
                                currentActiveCount,
                                activeLicense.LicenseFeatures.MaxUsers);
                            
                            return new UpdateUserResponse
                            {
                                IsSuccess = false,
                                ErrorMessage = $"Kullanıcı aktif hale getirilemedi. Kullanıcı limiti aşıldı. Maksimum: {activeLicense.LicenseFeatures.MaxUsers}, Mevcut: {currentActiveCount}"
                            };
                        }
                    }
                }
                
                existingUser.IsActive = request.IsActive;
                existingUser.IncludeInApplication = request.IncludeInApplication;
                existingUser.UpdatedBy = MngKeeper.Application.Common.Constants.SystemConstants.SystemUser; // TODO: Get from current user context
                existingUser.UpdatedAt = DateTime.UtcNow;

                // Save to database
                var updatedUser = await _userRepository.UpdateAsync(existingUser);

                // MO dizin profil cache'ini geçersiz kıl (ad/başlık/aktif değişmiş olabilir; best-effort).
                await _directoryCache.InvalidateUserAsync(claims.DomainId, updatedUser.Id, updatedUser.KeycloakUserId);

                // Invalidate user count cache if IsActive status changed
                if (wasActive != request.IsActive)
                {
                    await _licenseService.InvalidateUserCountCacheAsync(domain.Name);
                }

                // Sync to DataGateway MongoDB (mng_{domain} database) with custom data
                try
                {
                    await _dataGatewaySyncService.SyncUserToDataGatewayAsync(
                        updatedUser, 
                        claims.DomainId,
                        request.CustomData);
                    _logger.LogInformation("User synced to DataGateway: UserId={UserId}", updatedUser.Id);
                }
                catch (Exception syncEx)
                {
                    // Log error but don't fail the user update
                    _logger.LogError(syncEx, "Failed to sync user to DataGateway MongoDB: UserId={UserId}", updatedUser.Id);
                    // Continue - user is updated in MngKeeper DB
                }

                // Publish user updated event
                var userUpdatedEvent = new UserUpdatedEvent
                {
                    UserId = updatedUser.Id,
                    Username = updatedUser.Username,
                    Email = updatedUser.Email,
                    Groups = updatedUser.Groups
                };
                await EventPublishingHelper.PublishEventSafelyAsync(
                    _eventPublisher,
                    _logger,
                    userUpdatedEvent,
                    claims.DomainId,
                    "UserUpdatedEvent",
                    request.UserId);

                _logger.LogInformation("User updated successfully: {UserId} in domain: {DomainId}", request.UserId, claims.DomainId);

                return new UpdateUserResponse
                {
                    UserId = updatedUser.Id,
                    Username = updatedUser.Username,
                    Email = updatedUser.Email,
                    FirstName = updatedUser.FirstName,
                    LastName = updatedUser.LastName,
                    Title = updatedUser.Title,
                    Department = updatedUser.Department,
                    Gender = updatedUser.Gender,
                    PhoneNumber = updatedUser.PhoneNumber,
                    PhotoUrl = updatedUser.PhotoUrl,
                    GroupIds = updatedUser.Groups,
                    IsActive = updatedUser.IsActive,
                    IncludeInApplication = updatedUser.IncludeInApplication,
                    UpdatedAt = updatedUser.UpdatedAt ?? DateTime.UtcNow,
                    IsSuccess = true
                };
            }
            catch (Exception ex)
            {
                return ResponseHelper.CreateErrorResponse<UpdateUserResponse>(
                    _logger,
                    ex,
                    "UpdateUser",
                    request.UserId,
                    claims?.DomainId ?? "N/A");
            }
        }

        /// <summary>Directory kullanıcı — yalnızca uygulama alanları; Keycloak güncellenmez.</summary>
        private async Task<UpdateUserResponse> UpdateDirectoryUserAsync(
            UpdateUserCommand request,
            MngKeeper.Domain.Entities.User existingUser,
            TokenClaims claims)
        {
            existingUser.Title = request.Title;
            existingUser.Department = request.Department;
            existingUser.Gender = request.Gender;
            existingUser.PhoneNumber = request.PhoneNumber;
            UserPhotoProfileHelper.ApplyPhotoUrlFromRequest(existingUser, request.PhotoUrl);
            existingUser.IncludeInApplication = request.IncludeInApplication;
            existingUser.UpdatedBy = MngKeeper.Application.Common.Constants.SystemConstants.SystemUser;
            existingUser.UpdatedAt = DateTime.UtcNow;

            var updatedUser = await _userRepository.UpdateAsync(existingUser);

            // MO dizin profil cache'ini geçersiz kıl (best-effort/fail-open).
            await _directoryCache.InvalidateUserAsync(claims.DomainId, updatedUser.Id, updatedUser.KeycloakUserId);

            try
            {
                await _dataGatewaySyncService.SyncUserToDataGatewayAsync(
                    updatedUser,
                    claims.DomainId,
                    request.CustomData);
            }
            catch (Exception syncEx)
            {
                _logger.LogError(syncEx, "Failed to sync Directory user to DataGateway: UserId={UserId}", updatedUser.Id);
            }

            var userUpdatedEvent = new UserUpdatedEvent
            {
                UserId = updatedUser.Id,
                Username = updatedUser.Username,
                Email = updatedUser.Email,
                Groups = updatedUser.Groups
            };
            await EventPublishingHelper.PublishEventSafelyAsync(
                _eventPublisher,
                _logger,
                userUpdatedEvent,
                claims.DomainId,
                "UserUpdatedEvent",
                updatedUser.Id);

            return new UpdateUserResponse
            {
                UserId = updatedUser.Id,
                Username = updatedUser.Username,
                Email = updatedUser.Email ?? string.Empty,
                FirstName = updatedUser.FirstName,
                LastName = updatedUser.LastName,
                Title = updatedUser.Title,
                Department = updatedUser.Department,
                Gender = updatedUser.Gender,
                PhoneNumber = updatedUser.PhoneNumber,
                PhotoUrl = updatedUser.PhotoUrl,
                GroupIds = updatedUser.Groups,
                IsActive = updatedUser.IsActive,
                IncludeInApplication = updatedUser.IncludeInApplication,
                UpdatedAt = updatedUser.UpdatedAt ?? DateTime.UtcNow,
                IsSuccess = true
            };
        }
    }
}
