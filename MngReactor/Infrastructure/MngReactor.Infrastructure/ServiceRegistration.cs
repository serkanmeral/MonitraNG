using Microsoft.Extensions.DependencyInjection;
using MngReactor.Application.Abstractions.Ingest;
using MngReactor.Application.Abstractions.Observations;
using MngReactor.Application.Services;
using MngReactor.Domain.Interfaces;
using MngReactor.Infrastructure.Services;

namespace MngReactor.Infrastructure;

public static class ServiceRegistration
{
    public static void AddInfrastructureServices(this IServiceCollection services, MngReactor.Persistence.Settings.MngReactorSettings legacySettings)
    {
        services.AddSingleton<IMqttService>(_ =>
            new MqttService(
                legacySettings.MqttSettings.Host,
                legacySettings.MqttSettings.Port,
                legacySettings.MqttSettings.UserName,
                legacySettings.MqttSettings.Password));
        services.AddTransient<MqttAppService>();
        services.AddSingleton<IMetricPublisher, MetricPublisher>();
        services.AddSingleton<IObservationPublisher, ObservationPublisher>();
        services.AddSingleton<IIngestNotifyPublisher, IngestNotifyPublisher>();
        services.AddScoped<MngReactor.Application.Abstractions.Engine.IMqttSyncPublisher, MqttSyncPublisher>();
        services.AddHostedService<DomainCreatedEventConsumer>();
        services.AddHostedService<MonitoringSyncEventConsumer>();
    }
}
