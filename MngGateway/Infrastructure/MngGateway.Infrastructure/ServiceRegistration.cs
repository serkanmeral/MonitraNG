using Microsoft.Extensions.DependencyInjection;

namespace MngGateway.Infrastructure;

public static class ServiceRegistration
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services)
    {
        // Infrastructure services will be registered here
        // (e.g., logging, monitoring, etc.)

        return services;
    }
}

