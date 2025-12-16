using Microsoft.Extensions.Logging;
using MngKeeper.Application.Interfaces;
using MngKeeper.Domain.Entities;

namespace MngKeeper.Application.Pipelines.DomainCreation.Steps;

/// <summary>
/// Step 6: Create admin user and add to admins group
/// </summary>
public class CreateAdminUserStep : IPipelineStep<DomainCreationContext>
{
    private readonly IKeycloakService _keycloakService;
    private readonly IUserRepository _userRepository;
    private readonly IGroupRepository _groupRepository;
    private readonly IDataGatewaySyncService _dataGatewaySyncService;
    private readonly ILogger<CreateAdminUserStep> _logger;
    
    public string StepName => "CreateAdminUser";
    
    public CreateAdminUserStep(
        IKeycloakService keycloakService,
        IUserRepository userRepository,
        IGroupRepository groupRepository,
        IDataGatewaySyncService dataGatewaySyncService,
        ILogger<CreateAdminUserStep> logger)
    {
        _keycloakService = keycloakService;
        _userRepository = userRepository;
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
            _logger.LogInformation("Creating admin user for domain: {DomainName}", context.DomainName);
            
            // Create admin user
            var adminUserRequest = new CreateUserRequest
            {
                Username = $"{context.DomainName}_admin",
                Email = context.AdminEmail,
                Password = context.AdminPassword,
                FirstName = "Admin",
                LastName = context.DomainName,
                Groups = new List<string> { "admins" }
            };
            
            var userInfo = await _keycloakService.CreateUserAsync(context.RealmName, adminUserRequest);
            context.AdminUser = userInfo;
            
            _logger.LogInformation("Admin user created in Keycloak: {Username} ({UserId})", 
                adminUserRequest.Username, userInfo.Id);
            
            // Add user to admins group
            await _keycloakService.AddUserToGroupAsync(
                context.RealmName, 
                userInfo.Id, 
                MngKeeper.Application.Common.Constants.SystemGroups.Admins);
            
            _logger.LogInformation("Admin user added to {GroupName} group in Keycloak", MngKeeper.Application.Common.Constants.SystemGroups.Admins);
            
            // Get admins group from MngKeeper database (created in CreateDefaultGroupsStep)
            var domainGroups = await _groupRepository.GetByDomainIdAsync(context.Domain!.Id);
            var adminsGroup = domainGroups.FirstOrDefault(g => g.Name == MngKeeper.Application.Common.Constants.SystemGroups.Admins && g.DomainId == context.Domain.Id);
            
            if (adminsGroup == null)
            {
                _logger.LogWarning("Admins group not found in MngKeeper database for domain: {DomainId}", context.Domain.Id);
                // Continue anyway - user is created in Keycloak
            }
            
            // Create user entity (only for sync to domain database, not saved to mngkeeper database)
            // Note: User.Groups field stores group names (not IDs) for consistency with Keycloak
            var user = new User
            {
                Id = MongoDB.Bson.ObjectId.GenerateNewId().ToString(),
                KeycloakUserId = userInfo.Id,
                Username = adminUserRequest.Username,
                Email = adminUserRequest.Email,
                FirstName = adminUserRequest.FirstName,
                LastName = adminUserRequest.LastName,
                IsActive = true,
                Groups = new List<string> { "admins" }, // Store group name (consistent with Keycloak)
                DomainId = context.Domain.Id,
                CreatedBy = MngKeeper.Application.Common.Constants.SystemConstants.SystemUser,
                CreatedAt = DateTime.UtcNow
            };
            
            // Save to domain-specific database (users collection)
            var savedUser = await _userRepository.AddAsync(user);
            _logger.LogInformation("Admin user saved to domain database users collection: {UserId}", savedUser.Id);

            // Sync to domain database (@users collection for DataGateway)
            try
            {
                await _dataGatewaySyncService.SyncUserToDataGatewayAsync(
                    savedUser,
                    context.Domain.Id,
                    null);
                _logger.LogInformation("Admin user synced to domain database @users collection: {UserId}", savedUser.Id);
            }
            catch (Exception syncEx)
            {
                // Log error but don't fail the pipeline - user is created in Keycloak and domain database
                _logger.LogWarning(syncEx, "Failed to sync admin user to domain database @users collection, continuing...");
            }
            
            return StepResult.Success(new Dictionary<string, object>
            {
                ["adminUserId"] = userInfo.Id,
                ["adminUsername"] = adminUserRequest.Username,
                ["adminEmail"] = context.AdminEmail,
                ["userId"] = savedUser.Id
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create admin user");
            return StepResult.Failure($"Failed to create admin user: {ex.Message}", ex);
        }
    }
    
    public async Task RollbackAsync(
        DomainCreationContext context, 
        CancellationToken cancellationToken = default)
    {
        if (context.AdminUser != null)
        {
            _logger.LogWarning("Rollback: Deleting admin user {UserId}", context.AdminUser.Id);
            
            try
            {
                await _keycloakService.DeleteUserAsync(context.RealmName, context.AdminUser.Id);
                _logger.LogInformation("Admin user deleted successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete admin user during rollback");
            }
        }
    }
}

