using Microsoft.Extensions.DependencyInjection;
using MngNotifier.Application.Services;
using MngNotifier.Persistence.Services;

namespace MngNotifier.Persistence;

public static class ServiceRegistration
{
    public static void AddPersistenceServices(this IServiceCollection services)
    {
        // Health Check Service - Application health monitoring
        services.AddScoped<IHealthCheckService, HealthCheckService>();
    }
}
