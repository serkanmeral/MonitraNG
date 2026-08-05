using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MngAlarm.Application.Services;
using MngAlarm.Infrastructure.Clients;
using MngAlarm.Infrastructure.Http;
using MngAlarm.Infrastructure.Messaging;
using MngAlarm.Infrastructure.Persistence;
using MngAlarm.Infrastructure.Persistence.Repositories;
using MngAlarm.Infrastructure.Services;
using MngAlarm.Infrastructure.State;

namespace MngAlarm.Infrastructure;

public static class AlarmServiceCollectionExtensions
{
    public static IServiceCollection AddAlarmCore(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpClient("MngKeeper");
        services.AddHttpClient("MngDataGateway");
        services.AddHttpClient("MngHub");
        services.AddHttpClient("MngNotifiers");

        services.AddSingleton<IAlarmMongoContext, AlarmMongoContext>();
        services.AddSingleton<AlarmIndexInitializer>();
        services.AddSingleton<AlarmRabbitMqConnectionManager>();
        services.AddSingleton<AlarmTopologyBootstrapper>();
        services.AddSingleton<IAlarmEventPublisher, AlarmEventPublisher>();
        services.AddSingleton<IObservationIngressPublisher, ObservationIngressPublisher>();
        services.AddSingleton<ICorrelationWindowStore, MongoCorrelationWindowStore>();
        services.AddSingleton<IObservationActivityStore, MongoObservationActivityStore>();
        services.AddSingleton<ISequenceStateStore, MongoSequenceStateStore>();

        services.AddScoped<IAlarmRuleRepository, AlarmRuleRepository>();
        services.AddScoped<IScenarioRepository, ScenarioRepository>();
        services.AddScoped<IAlarmNotificationPolicyRepository, AlarmNotificationPolicyRepository>();
        services.AddScoped<IAlarmRepository, AlarmRepository>();
        services.AddScoped<IAlarmRuleService, AlarmRuleService>();
        services.AddScoped<IScenarioService, ScenarioService>();
        services.AddSingleton<IScenarioQueryProvider, UnavailableScenarioQueryProvider>();
        services.AddSingleton<IScenarioRuntimeCapabilities, ScenarioRuntimeCapabilities>();
        services.AddScoped<IScenarioSchedulerService, ScenarioSchedulerService>();
        services.AddSingleton<IScenarioPackageImportAuthorizer, ScenarioPackageImportAuthorizer>();
        services.AddScoped<IAlarmNotificationPolicyService, AlarmNotificationPolicyService>();
        services.AddSingleton<IAlarmNotificationCooldownStore, AlarmNotificationCooldownStore>();
        services.AddScoped<IAlarmDispatchTokenProvider, AlarmDispatchTokenProvider>();
        services.AddScoped<IAlarmOpNotificationsClient, AlarmOpNotificationsClient>();
        services.AddScoped<IAlarmHubNotificationClient, AlarmHubNotificationClient>();
        services.AddScoped<IAlarmNotifiersDispatchClient, AlarmNotifiersDispatchClient>();
        services.AddScoped<IAlarmKeeperUsersClient, AlarmKeeperUsersClient>();
        services.AddScoped<IAlarmNotificationDispatchService, AlarmNotificationDispatchService>();
        services.AddScoped<IAlarmQueryService, AlarmQueryService>();
        services.AddScoped<IAlarmLifecycleService, AlarmLifecycleService>();
        services.AddScoped<IObservationProcessor, ObservationProcessor>();
        services.AddScoped<IAlarmValidationService, AlarmValidationService>();
        services.AddHttpContextAccessor();
        services.AddScoped<IAlarmDomainAccessor, AlarmDomainAccessor>();
        services.AddScoped<IAlarmActorAccessor, AlarmActorAccessor>();

        return services;
    }

    public static IServiceCollection AddAlarmWorker(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddAlarmCore(configuration);
        services.AddHostedService<ObservationConsumer>();
        services.AddHostedService<MetricObservationBridgeConsumer>();
        return services;
    }
}
