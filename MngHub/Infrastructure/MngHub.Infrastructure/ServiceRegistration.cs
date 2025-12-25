using Microsoft.Extensions.DependencyInjection;
using MngHub.Application.Services;
using MngHub.Infrastructure.Services.Connection;
using MngHub.Infrastructure.Services.Jwt;
using MngHub.Infrastructure.Services.RabbitMq;
using MngHub.Infrastructure.Services.SignalR;
using MngHub.Infrastructure.Services.SystemEventListener;
using MngHub.Infrastructure.Services.GroupEventListener;

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

        // Message Router for SignalR
        services.AddScoped<MessageRouter>();

        // HTTP Client for JWT validation (if needed)
        services.AddHttpClient();

        // Background service for system event listening
        services.AddHostedService<SystemEventListenerService>();

        // Background service for group/user event listening
        services.AddHostedService<GroupEventListenerService>();

        return services;
    }
}

