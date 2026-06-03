using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MngAlarm.Application.Services;
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
        services.AddSingleton<IAlarmMongoContext, AlarmMongoContext>();
        services.AddSingleton<AlarmIndexInitializer>();
        services.AddSingleton<AlarmRabbitMqConnectionManager>();
        services.AddSingleton<AlarmTopologyBootstrapper>();
        services.AddSingleton<IAlarmEventPublisher, AlarmEventPublisher>();
        services.AddSingleton<IObservationIngressPublisher, ObservationIngressPublisher>();
        services.AddSingleton<ICorrelationWindowStore, MongoCorrelationWindowStore>();
        services.AddSingleton<IObservationActivityStore, MongoObservationActivityStore>();

        services.AddScoped<IAlarmRuleRepository, AlarmRuleRepository>();
        services.AddScoped<IAlarmRepository, AlarmRepository>();
        services.AddScoped<IAlarmRuleService, AlarmRuleService>();
        services.AddScoped<IAlarmQueryService, AlarmQueryService>();
        services.AddScoped<IObservationProcessor, ObservationProcessor>();
        services.AddScoped<IAlarmValidationService, AlarmValidationService>();
        services.AddHttpContextAccessor();
        services.AddScoped<IAlarmDomainAccessor, AlarmDomainAccessor>();

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
