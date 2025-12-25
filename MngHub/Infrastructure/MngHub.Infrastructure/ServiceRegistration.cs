using Microsoft.Extensions.DependencyInjection;
using MngHub.Application.Services;
using MngHub.Infrastructure.Services.Connection;
using MngHub.Infrastructure.Services.Jwt;
using MngHub.Infrastructure.Services.RabbitMq;
using MngHub.Infrastructure.Services.SystemEventListener;

namespace MngHub.Infrastructure;

public static class ServiceRegistration
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services)
    {
        // Connection Manager
        services.AddSingleton<IConnectionManager, ConnectionManager>();

        // RabbitMQ Consumer
        services.AddSingleton<IRabbitMqConsumer, RabbitMqConsumerService>();

        // JWT Validator
        services.AddScoped<IJwtValidator, JwtValidatorService>();

        // HTTP Client for JWT validation (if needed)
        services.AddHttpClient();

        // Background service for system event listening
        services.AddHostedService<SystemEventListenerService>();

        return services;
    }
}

