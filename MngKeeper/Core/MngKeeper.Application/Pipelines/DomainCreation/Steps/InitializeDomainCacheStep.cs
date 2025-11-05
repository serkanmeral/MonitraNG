using Microsoft.Extensions.Logging;
using MngKeeper.Application.Interfaces;
using System.Text.Json;

namespace MngKeeper.Application.Pipelines.DomainCreation.Steps;

/// <summary>
/// Step 9: Initialize Redis cache with users and groups (4 groups: admins, managers, users, guests)
/// </summary>
public class InitializeDomainCacheStep : IPipelineStep<DomainCreationContext>
{
    private readonly IRedisService _redisService;
    private readonly ILogger<InitializeDomainCacheStep> _logger;
    
    public string StepName => "InitializeDomainCache";
    
    public InitializeDomainCacheStep(
        IRedisService redisService,
        ILogger<InitializeDomainCacheStep> logger)
    {
        _redisService = redisService;
        _logger = logger;
    }
    
    public async Task<StepResult> ExecuteAsync(
        DomainCreationContext context, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Initializing Redis cache for domain: {DomainName}", context.DomainName);
            
            var domainName = context.DomainName;
            
            // Cache admin user
            if (context.AdminUser != null)
            {
                var userKey = $"domain:{domainName}:users";
                var userData = new
                {
                    id = context.AdminUser.Id,
                    email = context.AdminEmail,
                    firstName = "Admin",
                    lastName = context.DomainName,
                    username = $"{context.DomainName}_admin",
                    groups = new[]
                    {
                        new { id = context.AdminsGroup?.Id, name = "admins" }
                    }
                };
                
                await _redisService.SetAsync(
                    $"{userKey}:{context.AdminUser.Id}",
                    JsonSerializer.Serialize(userData),
                    TimeSpan.FromDays(365)  // Long TTL or no expiration
                );
                
                _logger.LogInformation("Admin user cached");
            }
            
            // Cache groups
            var groups = new[] 
            { 
                (context.AdminsGroup, "admins"), 
                (context.ManagersGroup, "managers"), 
                (context.UsersGroup, "users"),
                (context.GuestsGroup, "guests") 
            };
            
            var groupKey = $"domain:{domainName}:groups";
            
            foreach (var (group, name) in groups)
            {
                if (group != null)
                {
                    var groupData = new
                    {
                        id = group.Id,
                        name = name,
                        description = $"{name} group",
                        users = name == "admins" && context.AdminUser != null
                            ? new[]
                            {
                                new
                                {
                                    id = context.AdminUser.Id,
                                    email = context.AdminEmail,
                                    firstName = "Admin",
                                    lastName = context.DomainName
                                }
                            }
                            : Array.Empty<object>()
                    };
                    
                    await _redisService.SetAsync(
                        $"{groupKey}:{group.Id}",
                        JsonSerializer.Serialize(groupData),
                        TimeSpan.FromDays(365)
                    );
                    
                    _logger.LogInformation("Group cached: {GroupName}", name);
                }
            }
            
            // Cache metadata
            var metadataKey = $"domain:{domainName}:metadata";
            var metadata = new
            {
                usersLastUpdate = DateTime.UtcNow,
                groupsLastUpdate = DateTime.UtcNow,
                usersCount = 1,  // Admin user
                groupsCount = 4,  // admins, managers, users, guests
                status = "ready"
            };
            
            await _redisService.SetAsync(
                metadataKey,
                JsonSerializer.Serialize(metadata),
                TimeSpan.FromDays(365)
            );
            
            _logger.LogInformation("Domain cache initialized successfully");
            
            return StepResult.Success(new Dictionary<string, object>
            {
                ["cachedUsers"] = 1,
                ["cachedGroups"] = 4
            });
        }
        catch (Exception ex)
        {
            // Non-critical: log but don't fail the pipeline
            _logger.LogError(ex, "Failed to initialize domain cache (non-critical)");
            
            // Return success - cache can be populated later
            return StepResult.Success(new Dictionary<string, object>
            {
                ["warning"] = "Cache initialization failed but domain created"
            });
        }
    }
    
    public async Task RollbackAsync(
        DomainCreationContext context, 
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("Rollback: Clearing domain cache");
        
        try
        {
            var domainName = context.DomainName;
            
            // Delete specific cache keys
            await _redisService.DeleteAsync($"domain:{domainName}:users:*");
            await _redisService.DeleteAsync($"domain:{domainName}:groups:*");
            await _redisService.DeleteAsync($"domain:{domainName}:metadata");
            
            _logger.LogInformation("Domain cache cleared");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear domain cache during rollback");
        }
    }
}

