using Microsoft.Extensions.DependencyInjection;
using MngLogCollector.Application;
using MngLogCollector.Persistence;

namespace MngLogCollector.Infrastructure;

public static class InfrastructureServiceRegistration
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
    {
        services.AddApplicationServices();
        services.AddPersistenceServices();
        return services;
    }
}
