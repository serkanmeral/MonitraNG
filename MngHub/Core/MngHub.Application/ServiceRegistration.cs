using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Caching.Memory;
using MngHub.Application.Configuration;

namespace MngHub.Application;

public static class ServiceRegistration
{
    public static void AddApplicationServices(
        this IServiceCollection services,
        MngHubSettings settings)
    {
        // Configuration
        services.Configure<MngHubSettings>(_ =>
        {
            _.Server = settings.Server;
            _.RabbitMQ = settings.RabbitMQ;
            _.CertificateSettings = settings.CertificateSettings;
            _.OpenApiServerPath = settings.OpenApiServerPath;
            _.Actors = settings.Actors;
            _.SignalR = settings.SignalR;
            _.Connection = settings.Connection;
            _.Cors = settings.Cors;
            _.InternalNotifyApiKey = settings.InternalNotifyApiKey;
        });

        // Add Memory Cache for JWT validation results
        services.AddMemoryCache();
    }
}

