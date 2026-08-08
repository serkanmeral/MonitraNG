using Microsoft.Extensions.DependencyInjection;
using MngLogCollector.Application;
using MngLogCollector.Application.Abstractions.Observations;
using MngLogCollector.Infrastructure.Messaging;
using MngLogCollector.Persistence;

namespace MngLogCollector.Infrastructure;

public static class InfrastructureServiceRegistration
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
    {
        services.AddApplicationServices();
        services.AddPersistenceServices();
        services.AddSingleton<IAgentObservationPublisher, AgentObservationPublisher>();
        return services;
    }
}
