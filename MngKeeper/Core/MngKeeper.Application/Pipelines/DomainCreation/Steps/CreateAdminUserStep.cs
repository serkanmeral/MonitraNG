using Microsoft.Extensions.Logging;
using MngKeeper.Application.Interfaces;

namespace MngKeeper.Application.Pipelines.DomainCreation.Steps;

/// <summary>
/// Step 6: Create admin user and add to admins group
/// </summary>
public class CreateAdminUserStep : IPipelineStep<DomainCreationContext>
{
    private readonly IKeycloakService _keycloakService;
    private readonly ILogger<CreateAdminUserStep> _logger;
    
    public string StepName => "CreateAdminUser";
    
    public CreateAdminUserStep(
        IKeycloakService keycloakService,
        ILogger<CreateAdminUserStep> logger)
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
            
            _logger.LogInformation("Admin user created: {Username} ({UserId})", 
                adminUserRequest.Username, userInfo.Id);
            
            // Add user to admins group
            await _keycloakService.AddUserToGroupAsync(
                context.RealmName, 
                userInfo.Id, 
                "admins");
            
            _logger.LogInformation("Admin user added to admins group");
            
            return StepResult.Success(new Dictionary<string, object>
            {
                ["adminUserId"] = userInfo.Id,
                ["adminUsername"] = adminUserRequest.Username,
                ["adminEmail"] = context.AdminEmail
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

