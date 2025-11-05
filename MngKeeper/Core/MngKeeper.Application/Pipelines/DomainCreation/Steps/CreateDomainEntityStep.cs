using Microsoft.Extensions.Logging;
using MngKeeper.Application.Interfaces;
using MngKeeper.Domain.Entities;
using DomainEntity = MngKeeper.Domain.Entities.Domain;

namespace MngKeeper.Application.Pipelines.DomainCreation.Steps;

/// <summary>
/// Step 2: Create domain entity in MongoDB
/// </summary>
public class CreateDomainEntityStep : IPipelineStep<DomainCreationContext>
{
    private readonly IDomainRepository _domainRepository;
    private readonly ILogger<CreateDomainEntityStep> _logger;
    
    public string StepName => "CreateDomainEntity";
    
    public CreateDomainEntityStep(
        IDomainRepository domainRepository,
        ILogger<CreateDomainEntityStep> logger)
    {
        _domainRepository = domainRepository;
        _logger = logger;
    }
    
    public async Task<StepResult> ExecuteAsync(
        DomainCreationContext context, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Creating domain entity: {DomainName}", context.DomainName);
            
            var domain = new DomainEntity
            {
                Name = context.DomainName,
                DisplayName = context.DisplayName,
                DatabaseName = context.DatabaseName,
                RealmName = context.RealmName,
                StorageBucket = context.BucketName,
                Status = DomainStatus.Pending,  // Will be activated at the end
                Settings = new DomainSettings
                {
                    MaxUsers = context.Settings.MaxUsers,
                    MaxAssets = context.Settings.MaxAssets,
                    EnableMqtt = context.Settings.EnableMqtt,
                    MqttSettings = new MqttSettings
                    {
                        BrokerHost = context.Settings.MqttSettings.BrokerHost,
                        BrokerPort = context.Settings.MqttSettings.BrokerPort,
                        Username = context.Settings.MqttSettings.Username,
                        Password = context.Settings.MqttSettings.Password,
                        TopicPrefix = context.Settings.MqttSettings.TopicPrefix
                    },
                    CustomSettings = context.Settings.CustomSettings
                },
                StorageQuota = 10737418240,  // 10GB default
                StorageUsed = 0,
                CreatedBy = "system",  // TODO: Get from JWT context in future
                CreatedAt = DateTime.UtcNow
            };
            
            var savedDomain = await _domainRepository.AddAsync(domain);
            context.Domain = savedDomain;
            
            _logger.LogInformation("Domain entity created with ID: {DomainId}", savedDomain.Id);
            
            return StepResult.Success(new Dictionary<string, object>
            {
                ["domainId"] = savedDomain.Id,
                ["status"] = savedDomain.Status.ToString()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create domain entity");
            return StepResult.Failure("Failed to create domain entity", ex);
        }
    }
    
    public async Task RollbackAsync(
        DomainCreationContext context, 
        CancellationToken cancellationToken = default)
    {
        if (context.Domain != null)
        {
            _logger.LogWarning("Rollback: Deleting domain entity {DomainId}", context.Domain.Id);
            
            try
            {
                await _domainRepository.DeleteAsync(context.Domain.Id);
                _logger.LogInformation("Domain entity deleted successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete domain entity during rollback");
            }
        }
    }
}

