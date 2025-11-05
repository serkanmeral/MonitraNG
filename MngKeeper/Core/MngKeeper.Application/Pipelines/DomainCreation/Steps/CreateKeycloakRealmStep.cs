using Microsoft.Extensions.Logging;
using MngKeeper.Application.Interfaces;

namespace MngKeeper.Application.Pipelines.DomainCreation.Steps;

/// <summary>
/// Step 4: Create Keycloak realm for the domain
/// </summary>
public class CreateKeycloakRealmStep : IPipelineStep<DomainCreationContext>
{
    private readonly IKeycloakService _keycloakService;
    private readonly ILogger<CreateKeycloakRealmStep> _logger;
    
    public string StepName => "CreateKeycloakRealm";
    
    public CreateKeycloakRealmStep(
        IKeycloakService keycloakService,
        ILogger<CreateKeycloakRealmStep> logger)
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
            _logger.LogInformation("Creating Keycloak realm: {RealmName}", context.RealmName);
            
            var realmInfo = await _keycloakService.CreateRealmAsync(context.RealmName, context.Settings);
            context.RealmInfo = realmInfo;
            
            _logger.LogInformation("Keycloak realm created successfully: {RealmName}", context.RealmName);
            
            return StepResult.Success(new Dictionary<string, object>
            {
                ["realmName"] = context.RealmName,
                ["status"] = realmInfo.Status
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create Keycloak realm: {RealmName}", context.RealmName);
            return StepResult.Failure($"Failed to create Keycloak realm: {ex.Message}", ex);
        }
    }
    
    public async Task RollbackAsync(
        DomainCreationContext context, 
        CancellationToken cancellationToken = default)
    {
        if (context.RealmInfo != null)
        {
            _logger.LogWarning("Rollback: Deleting Keycloak realm {RealmName}", context.RealmName);
            
            try
            {
                await _keycloakService.DeleteRealmAsync(context.RealmName);
                _logger.LogInformation("Keycloak realm deleted successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete Keycloak realm during rollback");
            }
        }
    }
}

