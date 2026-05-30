using Microsoft.Extensions.DependencyInjection;
using MngOperations.Application.Configuration;
using MngOperations.Application.Diagnostics;
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

        services.AddHttpClient("MngScheduler", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(60);
        });

        services.AddHttpClient("MngKeeper", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(15);
        });

        // GEÇİCİ (perf/oc-optimization): istek başına downstream çağrı ölçümü.
        services.AddScoped<OcCallStats>();

        services.AddScoped<IMngDataGatewayClient, MngDataGatewayClient>();
        services.AddScoped<IKeeperDirectoryClient, MngKeeperClient>();
        services.AddScoped<IPersonDirectory, PersonDirectoryService>();
        services.AddScoped<IMngNotifiersClient, MngNotifiersClient>();
        services.AddScoped<IMngSchedulerClient, MngSchedulerClient>();
        services.AddScoped<INotificationOrchestrator, NotificationOrchestratorService>();
        services.AddScoped<INotificationQueryService, NotificationQueryService>();
        services.AddSingleton<IOcEventPublisher, OcEventPublisher>();
        services.AddScoped<IHealthCheckService, HealthCheckService>();
        services.AddScoped<IMetadataCache, MetadataCacheService>();
        services.AddScoped<ICatalogService, CatalogService>();
        services.AddScoped<IWorkItemKeyGenerator, WorkItemKeyGenerator>();
        services.AddScoped<IPermissionEvaluator, PermissionEvaluator>();
        services.AddScoped<IRuleEngine, RuleEngineService>();
        services.AddScoped<IFieldBehaviorResolver, FieldBehaviorResolverService>();
        services.AddScoped<ISlaCalculator, SlaCalculatorService>();
        services.AddScoped<IWorkItemTimelineService, WorkItemTimelineService>();
        services.AddScoped<IWorkItemCommandService, WorkItemCommandService>();
        services.AddScoped<IRuntimeContextService, RuntimeContextService>();
        services.AddScoped<IWorkItemScheduleSyncService, WorkItemScheduleSyncService>();
        services.AddScoped<IWorkItemScheduleExecuteService, WorkItemScheduleExecuteService>();

        return services;
    }
}
