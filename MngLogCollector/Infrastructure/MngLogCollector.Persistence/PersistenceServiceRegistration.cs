using Microsoft.Extensions.DependencyInjection;
using MngLogCollector.Application.Abstractions.OpenSearch;
using MngLogCollector.Persistence.OpenSearch;

namespace MngLogCollector.Persistence;

public static class PersistenceServiceRegistration
{
    public static IServiceCollection AddPersistenceServices(this IServiceCollection services)
    {
        services.AddHttpClient("opensearch", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
        });
        services.AddScoped<IOpenSearchBulkWriter, OpenSearchBulkWriter>();
        return services;
    }
}
