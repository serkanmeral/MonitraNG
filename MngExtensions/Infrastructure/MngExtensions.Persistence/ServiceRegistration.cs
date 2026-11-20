using Microsoft.Extensions.DependencyInjection;
using MngExtensions.Application.Pdks;
using MngExtensions.Persistence.Services;

namespace MngExtensions.Persistence;

public static class ServiceRegistration
{
    public static IServiceCollection AddPersistenceServices(this IServiceCollection services)
    {
        services.AddSingleton<IHealthCheckService, HealthCheckService>();
        return services;
    }
}
