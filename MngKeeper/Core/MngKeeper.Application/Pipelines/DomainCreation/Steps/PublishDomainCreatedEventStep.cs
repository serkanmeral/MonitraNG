using Microsoft.Extensions.Logging;
using MngKeeper.Application.Interfaces;
using System.Text.Json;

namespace MngKeeper.Application.Pipelines.DomainCreation.Steps;

/// <summary>
/// Step 7: Publish domain created event to RabbitMQ
/// </summary>
public class PublishDomainCreatedEventStep : IPipelineStep<DomainCreationContext>
{
    private readonly IRabbitMqService _rabbitMqService;
    private readonly ILogger<PublishDomainCreatedEventStep> _logger;
    
    public string StepName => "PublishDomainCreatedEvent";
    
    public PublishDomainCreatedEventStep(
        IRabbitMqService rabbitMqService,
        ILogger<PublishDomainCreatedEventStep> logger)
    {
        _rabbitMqService = rabbitMqService;
        _logger = logger;
    }
    
    public async Task<StepResult> ExecuteAsync(
        DomainCreationContext context, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Publishing domain created event: {DomainName}", context.DomainName);
            
            var eventMessage = new
            {
                eventId = Guid.NewGuid().ToString(),
                eventType = "system.mngkeeper.domain.created",
                timestamp = DateTime.UtcNow,
                source = "MngKeeper",
                version = "1.0",
                payload = new
                {
                    domainId = context.Domain!.Id,
                    domainName = context.DomainName,
                    databaseName = context.DatabaseName,
                    realmName = context.RealmName,
                    bucketName = context.BucketName,
                    status = "Active",
                    adminEmail = context.AdminEmail,
                    settings = new
                    {
                        maxUsers = context.Settings.MaxUsers,
                        maxAssets = context.Settings.MaxAssets,
                        enableMqtt = context.Settings.EnableMqtt
                    },
                    createdAt = context.Domain.CreatedAt
                }
            };
            
            var message = JsonSerializer.Serialize(eventMessage);
            
            await _rabbitMqService.PublishAsync(
                exchange: "mng.topics",
                routingKey: "system.mngkeeper.domain.created",
                message: message);
            
            _logger.LogInformation("Domain created event published successfully");
            
            return StepResult.Success(new Dictionary<string, object>
            {
                ["eventId"] = eventMessage.eventId,
                ["routingKey"] = "system.mngkeeper.domain.created"
            });
        }
        catch (Exception ex)
        {
            // Non-critical: log but don't fail the pipeline
            _logger.LogError(ex, "Failed to publish domain created event (non-critical)");
            
            // Return success even if RabbitMQ publish fails
            // The domain is still created successfully
            return StepResult.Success(new Dictionary<string, object>
            {
                ["warning"] = "Event publishing failed but domain created"
            });
        }
    }
    
    public Task RollbackAsync(
        DomainCreationContext context, 
        CancellationToken cancellationToken = default)
    {
        // Cannot rollback a published message (fire-and-forget)
        // Consumers should handle domain.deleted event if we implement domain deletion
        _logger.LogInformation("Rollback: PublishDomainCreatedEvent (no action needed - fire-and-forget)");
        return Task.CompletedTask;
    }
}

