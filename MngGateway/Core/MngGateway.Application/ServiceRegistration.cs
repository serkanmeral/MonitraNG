using MngGateway.Application.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MngGateway.Application;

public static class ServiceRegistration
{
    public static IServiceCollection AddApplicationServices(
        this IServiceCollection services,
        MngGatewaySettings settings)
    {
        // Register settings as singleton
        services.AddSingleton(settings);

        return services;
    }
}

