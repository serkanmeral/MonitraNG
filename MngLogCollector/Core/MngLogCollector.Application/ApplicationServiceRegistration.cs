using Microsoft.Extensions.DependencyInjection;
using MngLogCollector.Application.Abstractions.Ingest;
using MngLogCollector.Application.Abstractions.Policy;
using MngLogCollector.Application.Services.Ingest;
using MngLogCollector.Application.Services.Policy;

namespace MngLogCollector.Application;

public static class ApplicationServiceRegistration
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IIngestBatchService, IngestBatchService>();
        services.AddSingleton<IEventLogPackageCatalogService, BuiltinEventLogPackageCatalogService>();
        return services;
    }
}
