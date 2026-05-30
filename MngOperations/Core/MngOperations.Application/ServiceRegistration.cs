using Microsoft.Extensions.DependencyInjection;
using MngOperations.Application.Configuration;

namespace MngOperations.Application;

public static class ServiceRegistration
{
    public static IServiceCollection AddApplicationServices(
        this IServiceCollection services,
        MngOperationsSettings settings)
    {
        services.Configure<MngOperationsSettings>(_ =>
        {
            _.Server = settings.Server;
            _.OpenApiServerPath = settings.OpenApiServerPath;
            _.PerfDiagnostics = settings.PerfDiagnostics;
            _.Actors = settings.Actors;
            _.DataGateway = settings.DataGateway;
            _.MngNotifiers = settings.MngNotifiers;
            _.RabbitMq = settings.RabbitMq;
            _.MetadataCache = settings.MetadataCache;
        });

        return services;
    }
}
