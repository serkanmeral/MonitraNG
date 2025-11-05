using Microsoft.Extensions.Logging;
using MngKeeper.Application.Interfaces;

namespace MngKeeper.Application.Pipelines.DomainCreation.Steps;

/// <summary>
/// Step 5: Create default groups (admins, managers, users, guests)
/// </summary>
public class CreateDefaultGroupsStep : IPipelineStep<DomainCreationContext>
{
    private readonly IKeycloakService _keycloakService;
    private readonly ILogger<CreateDefaultGroupsStep> _logger;
    
    public string StepName => "CreateDefaultGroups";
    
    public CreateDefaultGroupsStep(
        IKeycloakService keycloakService,
        ILogger<CreateDefaultGroupsStep> logger)
    {
        _keycloakService = keycloakService;
        _logger = logger;
    }
    
    public async Task<StepResult> ExecuteAsync(
        DomainCreationContext context, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Creating default groups for realm: {RealmName}", context.RealmName);
            
            // Create admins group
            var adminsGroup = await _keycloakService.CreateGroupAsync(
                context.RealmName,
                new CreateGroupRequest
                {
                    Name = "admins",
                    Description = "Administrators group"
                });
            context.AdminsGroup = adminsGroup;
            _logger.LogInformation("Created group: admins");
            
            // Create managers group
            var managersGroup = await _keycloakService.CreateGroupAsync(
                context.RealmName,
                new CreateGroupRequest
                {
                    Name = "managers",
                    Description = "Managers group"
                });
            context.ManagersGroup = managersGroup;
            _logger.LogInformation("Created group: managers");
            
            // Create users group
            var usersGroup = await _keycloakService.CreateGroupAsync(
                context.RealmName,
                new CreateGroupRequest
                {
                    Name = "users",
                    Description = "Standard users group"
                });
            context.UsersGroup = usersGroup;
            _logger.LogInformation("Created group: users");
            
            // Create guests group
            var guestsGroup = await _keycloakService.CreateGroupAsync(
                context.RealmName,
                new CreateGroupRequest
                {
                    Name = "guests",
                    Description = "Guests group"
                });
            context.GuestsGroup = guestsGroup;
            _logger.LogInformation("Created group: guests");
            
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
                    await _keycloakService.DeleteGroupAsync(context.RealmName, group.Id);
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

