using Microsoft.Extensions.DependencyInjection;
using MngLogCollector.Application.Abstractions.Ingest;
using MngLogCollector.Application.Services.Ingest;

namespace MngLogCollector.Application;

public static class ApplicationServiceRegistration
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IIngestBatchService, IngestBatchService>();
        return services;
    }
}
