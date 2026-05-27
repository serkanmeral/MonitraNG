using Microsoft.Extensions.DependencyInjection;
using MngOperations.Application.Configuration;
using MngOperations.Application.Interfaces;
using MngOperations.Infrastructure.Clients;
using MngOperations.Infrastructure.Services;

namespace MngOperations.Infrastructure;

public static class ServiceRegistration
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        MngOperationsSettings settings)
    {
        services.AddHttpClient("MngDataGateway", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(120);
        });

        services.AddHttpClient("HealthCheck", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(5);
        });

        services.AddHttpClient("MngNotifiers", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        services.AddScoped<IMngDataGatewayClient, MngDataGatewayClient>();
        services.AddScoped<IMngNotifiersClient, MngNotifiersClient>();
        services.AddScoped<INotificationOrchestrator, NotificationOrchestratorService>();
        services.AddSingleton<IOcEventPublisher, OcEventPublisher>();
        services.AddScoped<IHealthCheckService, HealthCheckService>();
        services.AddScoped<IMetadataCache, MetadataCacheService>();
        services.AddScoped<IWorkItemKeyGenerator, WorkItemKeyGenerator>();
        services.AddScoped<IPermissionEvaluator, PermissionEvaluator>();
        services.AddScoped<IRuleEngine, RuleEngineService>();
        services.AddScoped<IFieldBehaviorResolver, FieldBehaviorResolverService>();
        services.AddScoped<ISlaCalculator, SlaCalculatorService>();
        services.AddScoped<IWorkItemTimelineService, WorkItemTimelineService>();
        services.AddScoped<IWorkItemCommandService, WorkItemCommandService>();
        services.AddScoped<IRuntimeContextService, RuntimeContextService>();

        return services;
    }
}
