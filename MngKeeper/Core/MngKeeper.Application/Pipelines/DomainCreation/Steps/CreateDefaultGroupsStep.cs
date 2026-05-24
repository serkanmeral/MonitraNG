using Microsoft.Extensions.Logging;
using MngKeeper.Application.Interfaces;
using MngKeeper.Domain.Entities;
using MngKeeper.Domain.Enums;

namespace MngKeeper.Application.Pipelines.DomainCreation.Steps;

/// <summary>
/// Step 5: Create default groups (admins, managers, users, guests)
/// </summary>
public class CreateDefaultGroupsStep : IPipelineStep<DomainCreationContext>
{
    private readonly IKeycloakService _keycloakService;
    private readonly IGroupRepository _groupRepository;
    private readonly IDataGatewaySyncService _dataGatewaySyncService;
    private readonly ILogger<CreateDefaultGroupsStep> _logger;
    
    public string StepName => "CreateDefaultGroups";
    
    public CreateDefaultGroupsStep(
        IKeycloakService keycloakService,
        IGroupRepository groupRepository,
        IDataGatewaySyncService dataGatewaySyncService,
        ILogger<CreateDefaultGroupsStep> logger)
    {
        _keycloakService = keycloakService;
        _groupRepository = groupRepository;
        _dataGatewaySyncService = dataGatewaySyncService;
        _logger = logger;
    }
    
    public async Task<StepResult> ExecuteAsync(
        DomainCreationContext context, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (context.Domain == null)
            {
                return StepResult.Failure("Domain entity not found in context. Cannot create groups without domain.");
            }

            _logger.LogInformation("Creating default groups for realm: {RealmName}, domain: {DomainId}", context.RealmName, context.Domain.Id);
            
            // Helper method to create group in both Keycloak and MngKeeper DB
            async Task<GroupInfo> CreateGroupInBothSystems(string name, string description)
            {
                // Create in Keycloak
                var keycloakGroup = await _keycloakService.CreateGroupAsync(
                    context.RealmName,
                    new CreateGroupRequest
                    {
                        Name = name,
                        Description = description
                    });

                // Create group entity (only for sync to domain database, not saved to mngkeeper database)
                var group = new Group
                {
                    Id = MongoDB.Bson.ObjectId.GenerateNewId().ToString(),
                    Name = name,
                    Description = description,
                    Permissions = new List<string>(),
                    IsActive = true,
                    DomainId = context.Domain.Id,
                    KeycloakGroupId = keycloakGroup.Id ?? string.Empty,
                    ProvisioningSource = UserProvisioningSource.Local,
                    CreatedBy = MngKeeper.Application.Common.Constants.SystemConstants.SystemUser,
                    CreatedAt = DateTime.UtcNow
                };

                // Save to domain-specific database (groups collection)
                var savedGroup = await _groupRepository.AddAsync(group);
                _logger.LogInformation("Group saved to domain database groups collection: {Name} (ID: {GroupId})", name, savedGroup.Id);

                // Sync to domain database (@groups collection for DataGateway)
                try
                {
                    await _dataGatewaySyncService.SyncGroupToDataGatewayAsync(
                        savedGroup,
                        context.Domain.Id,
                        null);
                    _logger.LogInformation("Group synced to domain database @groups collection: {Name} (ID: {GroupId})", name, savedGroup.Id);
                }
                catch (Exception syncEx)
                {
                    _logger.LogWarning(syncEx, "Failed to sync group {Name} to domain database @groups collection, continuing...", name);
                }

                return keycloakGroup;
            }
            
            // Create admins group
            var adminsGroup = await CreateGroupInBothSystems(MngKeeper.Application.Common.Constants.SystemGroups.Admins, "Administrators group");
            context.AdminsGroup = adminsGroup;
            _logger.LogInformation("Created group: {GroupName}", MngKeeper.Application.Common.Constants.SystemGroups.Admins);
            
            // Create managers group
            var managersGroup = await CreateGroupInBothSystems(MngKeeper.Application.Common.Constants.SystemGroups.Managers, "Managers group");
            context.ManagersGroup = managersGroup;
            _logger.LogInformation("Created group: {GroupName}", MngKeeper.Application.Common.Constants.SystemGroups.Managers);
            
            // Create users group
            var usersGroup = await CreateGroupInBothSystems(MngKeeper.Application.Common.Constants.SystemGroups.Users, "Standard users group");
            context.UsersGroup = usersGroup;
            _logger.LogInformation("Created group: {GroupName}", MngKeeper.Application.Common.Constants.SystemGroups.Users);
            
            // Create guests group
            var guestsGroup = await CreateGroupInBothSystems(MngKeeper.Application.Common.Constants.SystemGroups.Guests, "Guests group");
            context.GuestsGroup = guestsGroup;
            _logger.LogInformation("Created group: {GroupName}", MngKeeper.Application.Common.Constants.SystemGroups.Guests);
            
            return StepResult.Success(new Dictionary<string, object>
            {
                ["adminsGroupId"] = adminsGroup.Id,
                ["managersGroupId"] = managersGroup.Id,
                ["usersGroupId"] = usersGroup.Id,
                ["guestsGroupId"] = guestsGroup.Id
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create default groups");
            return StepResult.Failure($"Failed to create default groups: {ex.Message}", ex);
        }
    }
    
    public async Task RollbackAsync(
        DomainCreationContext context, 
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("Rollback: Deleting default groups");
        
        var groups = new[] { context.AdminsGroup, context.ManagersGroup, context.UsersGroup, context.GuestsGroup };
        
        foreach (var group in groups)
        {
            if (group != null)
            {
                try
                {
                    await _keycloakService.DeleteGroupAsync(context.RealmName, group.Name);
                    _logger.LogInformation("Deleted group: {GroupName}", group.Name);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to delete group {GroupName} during rollback", group.Name);
                }
            }
        }
    }
}

