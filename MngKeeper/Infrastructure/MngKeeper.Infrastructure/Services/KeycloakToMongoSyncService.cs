using System.Diagnostics;
using MngKeeper.Application.Configuration;
using MngKeeper.Application.Directory;
using MngKeeper.Application.Interfaces;
using MngKeeper.Domain.Entities;
using MngKeeper.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Bson;

namespace MngKeeper.Infrastructure.Services;

public class KeycloakToMongoSyncService : IKeycloakToMongoSyncService
{
    private readonly IDomainRepository _domainRepository;
    private readonly IUserRepository _userRepository;
    private readonly IGroupRepository _groupRepository;
    private readonly IKeycloakService _keycloakService;
    private readonly IDirectorySyncCoordinator _coordinator;
    private readonly DirectorySyncSettings _directorySyncSettings;
    private readonly IUserPhotoProfileService _photoProfileService;
    private readonly ILogger<KeycloakToMongoSyncService> _logger;

    public KeycloakToMongoSyncService(
        IDomainRepository domainRepository,
        IUserRepository userRepository,
        IGroupRepository groupRepository,
        IKeycloakService keycloakService,
        IDirectorySyncCoordinator coordinator,
        IUserPhotoProfileService photoProfileService,
        IOptions<MngKeeperSettings> settings,
        ILogger<KeycloakToMongoSyncService> logger)
    {
        _domainRepository = domainRepository;
        _userRepository = userRepository;
        _groupRepository = groupRepository;
        _keycloakService = keycloakService;
        _coordinator = coordinator;
        _photoProfileService = photoProfileService;
        _directorySyncSettings = settings.Value.DirectorySync;
        _logger = logger;
    }

    public async Task<DirectorySyncResult> SyncUserOnLoginAsync(
        string domainId,
        string username,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        if (!_directorySyncSettings.LoginSyncEnabled)
        {
            return new DirectorySyncResult
            {
                IsSuccess = true,
                Code = "login_sync_disabled",
                Message = "Login directory sync is disabled.",
                TriggeredBy = DirectorySyncTrigger.Login.ToString(),
                DomainId = domainId,
                DurationMs = sw.ElapsedMilliseconds
            };
        }

        var domain = await ResolveDomainAsync(domainId);
        if (domain == null)
        {
            return new DirectorySyncResult
            {
                IsSuccess = false,
                Code = "domain_not_found",
                Message = $"Domain not found: {domainId}",
                TriggeredBy = DirectorySyncTrigger.Login.ToString(),
                DurationMs = sw.ElapsedMilliseconds
            };
        }

        if (!_coordinator.TryBeginSync(domain.Id))
        {
            _logger.LogDebug(
                "Login sync skipped for {Username} — full directory sync in progress for domain {DomainId}",
                username, domain.Id);
            return new DirectorySyncResult
            {
                IsSuccess = true,
                Code = "sync_in_progress",
                Message = "Full directory sync in progress; login sync skipped.",
                TriggeredBy = DirectorySyncTrigger.Login.ToString(),
                DomainId = domain.Id,
                RealmName = domain.RealmName,
                DurationMs = sw.ElapsedMilliseconds
            };
        }

        try
        {
            return await SyncSingleUserCoreAsync(domain, username.Trim(), cancellationToken);
        }
        finally
        {
            _coordinator.EndSync(domain.Id);
        }
    }

    private async Task<DirectorySyncResult> SyncSingleUserCoreAsync(
        MngKeeper.Domain.Entities.Domain domain,
        string username,
        CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        var result = new DirectorySyncResult
        {
            TriggeredBy = DirectorySyncTrigger.Login.ToString(),
            DomainId = domain.Id,
            RealmName = domain.RealmName
        };

        try
        {
            var kcUser = await _keycloakService.GetRealmUserByUsernameAsync(
                domain.RealmName, username, cancellationToken);

            if (kcUser == null || string.IsNullOrWhiteSpace(kcUser.Id))
            {
                result.IsSuccess = false;
                result.Code = "keycloak_user_not_found";
                result.Message = $"Keycloak user not found: {username}";
                result.DurationMs = sw.ElapsedMilliseconds;
                return result;
            }

            var existing =
                await _userRepository.GetByKeycloakUserIdAsync(kcUser.Id, domain.Id)
                ?? await _userRepository.GetByUsernameAsync(kcUser.Username, domain.Id);

            if (existing != null && existing.ProvisioningSource == UserProvisioningSource.Local)
            {
                result.IsSuccess = true;
                result.Code = "skipped_local_user";
                result.Message = "Local (break-glass) user; login sync skipped.";
                result.UsersSkipped = 1;
                result.DurationMs = sw.ElapsedMilliseconds;
                return result;
            }

            var groupNames = await _keycloakService.GetUserGroupNamesAsync(
                domain.RealmName, kcUser.Id, cancellationToken);

            if (!DirectoryUserSyncComparer.ShouldSyncFromKeycloak(existing, kcUser, groupNames))
            {
                if (existing != null)
                    await TrySyncUserPhotoAsync(existing, domain, cancellationToken);

                result.IsSuccess = true;
                result.Code = "unchanged";
                result.Message = "Mongo user already matches Keycloak.";
                result.UsersSkipped = 1;
                result.DurationMs = sw.ElapsedMilliseconds;
                return result;
            }

            var syncedAt = DateTime.UtcNow;
            User? syncedUser;
            if (existing == null)
            {
                var user = DirectoryUserFieldSets.CreateDirectoryUser(domain.Id, kcUser, groupNames, syncedAt);
                await _userRepository.AddAsync(user);
                syncedUser = user;
                result.UsersCreated = 1;
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(existing.KeycloakUserId) &&
                    !string.Equals(existing.KeycloakUserId, kcUser.Id, StringComparison.Ordinal))
                {
                    _logger.LogWarning(
                        "Login sync: Keycloak user id changed for {Username} in domain {DomainId}: {OldId} → {NewId}",
                        username, domain.Id, existing.KeycloakUserId, kcUser.Id);
                }

                DirectoryUserFieldSets.ApplyDirectoryFields(existing, kcUser, groupNames, syncedAt);
                await _userRepository.UpdateAsync(existing);
                syncedUser = existing;
                result.UsersUpdated = 1;
            }

            if (syncedUser != null)
                await TrySyncUserPhotoAsync(syncedUser, domain, cancellationToken);

            result.IsSuccess = true;
            result.Code = "success";
            result.Message = "Login user sync completed.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Login sync failed for {Username} domain {DomainId}", username, domain.Id);
            result.IsSuccess = false;
            result.Code = "keycloak_error";
            result.Message = ex.Message;
            result.Errors.Add(ex.Message);
        }

        result.DurationMs = sw.ElapsedMilliseconds;
        _logger.LogInformation(
            "Login sync {Code} user={Username} domain={DomainId} created={Created} updated={Updated} skip={Skip} ms={Ms}",
            result.Code, username, domain.Id,
            result.UsersCreated, result.UsersUpdated, result.UsersSkipped, result.DurationMs);

        return result;
    }

    public async Task<DirectorySyncResult> SyncDomainAsync(
        string domainId,
        DirectorySyncTrigger trigger,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var result = new DirectorySyncResult
        {
            TriggeredBy = trigger.ToString(),
            DomainId = domainId
        };

        var domain = await ResolveDomainAsync(domainId);
        if (domain == null)
        {
            result.IsSuccess = false;
            result.Code = "domain_not_found";
            result.Message = $"Domain not found (id, name or realm): {domainId}";
            result.DurationMs = sw.ElapsedMilliseconds;
            _logger.LogWarning(
                "[DirectorySync] Domain not found for key={DomainKey} trigger={Trigger}",
                domainId, trigger);
            return result;
        }

        var resolvedDomainId = domain.Id;
        result.DomainId = resolvedDomainId;
        result.RealmName = domain.RealmName;
        var realm = domain.RealmName;
        var syncedAt = DateTime.UtcNow;

        _logger.LogInformation(
            "[DirectorySync] Keycloak→Mongo sync started domain={DomainId} realm={Realm} trigger={Trigger}",
            resolvedDomainId, realm, trigger);

        try
        {
            var kcGroups = await _keycloakService.ListRealmGroupsAsync(realm, cancellationToken);
            _logger.LogInformation(
                "[DirectorySync] Keycloak groups listed realm={Realm} count={Count}",
                realm, kcGroups.Count);
            foreach (var kcGroup in kcGroups)
            {
                if (string.IsNullOrWhiteSpace(kcGroup.Name))
                    continue;

                var existing = await _groupRepository.GetByNameAsync(kcGroup.Name, resolvedDomainId);
                if (existing == null)
                {
                    var group = new Group
                    {
                        Id = ObjectId.GenerateNewId().ToString(),
                        Name = kcGroup.Name,
                        DomainId = resolvedDomainId,
                        KeycloakGroupId = kcGroup.Id,
                        IsActive = true,
                        IncludeInApplication = ApplicationScopeDefaults.DefaultForSource(UserProvisioningSource.Directory),
                        ProvisioningSource = UserProvisioningSource.Directory,
                        DirectorySyncedAt = syncedAt,
                        CreatedAt = syncedAt,
                        CreatedBy = MngKeeper.Application.Common.Constants.SystemConstants.SystemUser
                    };
                    await _groupRepository.AddAsync(group);
                    result.GroupsCreated++;
                }
                else if (
                    existing.ProvisioningSource == UserProvisioningSource.Local
                    && DirectoryGroupPolicy.IsProtectedLocalGroupName(existing.Name))
                {
                    // admins / managers / users / guests — MonitraNG yerel grupları
                    if (string.IsNullOrWhiteSpace(existing.KeycloakGroupId) && !string.IsNullOrWhiteSpace(kcGroup.Id))
                    {
                        existing.KeycloakGroupId = kcGroup.Id;
                        existing.UpdatedAt = syncedAt;
                        existing.UpdatedBy = MngKeeper.Application.Common.Constants.SystemConstants.SystemUser;
                        await _groupRepository.UpdateAsync(existing);
                        result.GroupsUpdated++;
                    }
                }
                else
                {
                    // KC realm'de olan diğer tüm gruplar (eski kayıtlar dahil) → Directory
                    var changed = false;
                    if (existing.ProvisioningSource != UserProvisioningSource.Directory)
                    {
                        existing.ProvisioningSource = UserProvisioningSource.Directory;
                        changed = true;
                        _logger.LogInformation(
                            "[DirectorySync] Group promoted to Directory: {GroupName} domain={DomainId}",
                            existing.Name, resolvedDomainId);
                    }
                    if (existing.KeycloakGroupId != kcGroup.Id)
                    {
                        existing.KeycloakGroupId = kcGroup.Id;
                        changed = true;
                    }
                    if (!existing.IsActive)
                    {
                        existing.IsActive = true;
                        changed = true;
                    }
                    if (existing.DirectorySyncedAt != syncedAt)
                    {
                        existing.DirectorySyncedAt = syncedAt;
                        changed = true;
                    }
                    if (changed)
                    {
                        existing.UpdatedAt = syncedAt;
                        existing.UpdatedBy = MngKeeper.Application.Common.Constants.SystemConstants.SystemUser;
                        await _groupRepository.UpdateAsync(existing);
                        result.GroupsUpdated++;
                    }
                }
            }

            var kcUsers = await _keycloakService.ListRealmUsersAsync(realm, cancellationToken);
            _logger.LogInformation(
                "[DirectorySync] Keycloak users listed realm={Realm} count={Count}",
                realm, kcUsers.Count);
            var syncedKeycloakIds = new HashSet<string>(StringComparer.Ordinal);

            foreach (var kcUser in kcUsers)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (string.IsNullOrWhiteSpace(kcUser.Id) || string.IsNullOrWhiteSpace(kcUser.Username))
                    continue;

                syncedKeycloakIds.Add(kcUser.Id);

                var existing =
                    await _userRepository.GetByKeycloakUserIdAsync(kcUser.Id, resolvedDomainId)
                    ?? await _userRepository.GetByUsernameAsync(kcUser.Username, resolvedDomainId);

                if (existing != null && existing.ProvisioningSource == UserProvisioningSource.Local)
                {
                    result.UsersSkipped++;
                    _logger.LogDebug("Skipping local (break-glass) user during directory sync: {Username}", kcUser.Username);
                    continue;
                }

                var groupNames = await _keycloakService.GetUserGroupNamesAsync(realm, kcUser.Id, cancellationToken);

                if (existing == null)
                {
                    var user = DirectoryUserFieldSets.CreateDirectoryUser(resolvedDomainId, kcUser, groupNames, syncedAt);
                    await _userRepository.AddAsync(user);
                    await TrySyncUserPhotoAsync(user, domain, cancellationToken);
                    result.UsersCreated++;
                }
                else
                {
                    DirectoryUserFieldSets.ApplyDirectoryFields(existing, kcUser, groupNames, syncedAt);
                    await _userRepository.UpdateAsync(existing);
                    await TrySyncUserPhotoAsync(existing, domain, cancellationToken);
                    result.UsersUpdated++;
                }
            }

            var mongoUsers = await _userRepository.GetByDomainIdAsync(resolvedDomainId);
            foreach (var mongoUser in mongoUsers)
            {
                if (mongoUser.ProvisioningSource != UserProvisioningSource.Directory)
                    continue;

                if (string.IsNullOrWhiteSpace(mongoUser.KeycloakUserId))
                    continue;

                if (syncedKeycloakIds.Contains(mongoUser.KeycloakUserId))
                    continue;

                if (mongoUser.IsActive)
                {
                    mongoUser.IsActive = false;
                    mongoUser.DirectorySyncedAt = syncedAt;
                    mongoUser.UpdatedAt = syncedAt;
                    mongoUser.UpdatedBy = MngKeeper.Application.Common.Constants.SystemConstants.SystemUser;
                    await _userRepository.UpdateAsync(mongoUser);
                    result.UsersDeactivated++;
                }
            }

            // KC→Mongo zaten @users/@groups'a BsonDocument ile yazıldı; toplu DataGateway sync burada gerekmez
            // (__syncInfo istenirse ayrı POST /api/sync/users|groups kullanılabilir)

            result.IsSuccess = true;
            result.Code = "success";
            result.Message = "Directory sync completed.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[DirectorySync] Sync failed domain={DomainId} realm={Realm}", resolvedDomainId, realm);
            result.IsSuccess = false;
            result.Code = "keycloak_error";
            result.Message = ex.Message;
            result.Errors.Add(ex.Message);
        }

        result.DurationMs = sw.ElapsedMilliseconds;
        _logger.LogInformation(
            "[DirectorySync] Sync finished code={Code} domain={DomainId} realm={Realm} trigger={Trigger} " +
            "groups +{GC}/~{GU} users +{UC}/~{UU} skip={Skip} deactivated={Deact} ms={Ms}",
            result.Code, resolvedDomainId, result.RealmName, trigger,
            result.GroupsCreated, result.GroupsUpdated,
            result.UsersCreated, result.UsersUpdated,
            result.UsersSkipped, result.UsersDeactivated, result.DurationMs);

        return result;
    }

    /// <summary>
    /// <paramref name="domainIdOrName"/> Mongo <c>domains._id</c>, <c>name</c> veya <c>realmName</c> (ör. odak).
    /// </summary>
    private async Task<MngKeeper.Domain.Entities.Domain?> ResolveDomainAsync(string domainIdOrName)
    {
        var key = (domainIdOrName ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(key))
            return null;

        if (ObjectId.TryParse(key, out _))
        {
            var byId = await _domainRepository.GetByIdAsync(key);
            if (byId != null)
                return byId;
        }

        var byName = await _domainRepository.GetByNameAsync(key);
        if (byName != null)
            return byName;

        return await _domainRepository.GetByRealmNameAsync(key);
    }

    private async Task TrySyncUserPhotoAsync(
        User user,
        MngKeeper.Domain.Entities.Domain domain,
        CancellationToken cancellationToken)
    {
        try
        {
            if (await _photoProfileService.TryImportDirectoryPhotoAsync(user, domain, cancellationToken))
                await _userRepository.UpdateAsync(user);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Directory photo sync skipped for user {UserId} ({Username})",
                user.Id,
                user.Username);
        }
    }
}
