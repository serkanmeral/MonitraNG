using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using MngLogCollector.Application.Abstractions.Discovery;
using MngLogCollector.Application.Abstractions.OpenSearch;
using MngLogCollector.Application.Abstractions.Policy;
using MngLogCollector.Application.Configuration;
using MngLogCollector.Persistence.Discovery;
using MngLogCollector.Persistence.OpenSearch;
using MngLogCollector.Persistence.Policy;

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

        services.AddSingleton<IMongoClient>(sp =>
        {
            var mongo = sp.GetRequiredService<IOptions<MngLogCollectorSettings>>().Value.MongoDB;
            var cs = mongo.ConnectionString?.Trim();
            if (string.IsNullOrEmpty(cs))
            {
                var auth = string.IsNullOrEmpty(mongo.Username)
                    ? ""
                    : $"{Uri.EscapeDataString(mongo.Username)}:{Uri.EscapeDataString(mongo.Password ?? "")}@";
                cs = $"mongodb://{auth}{mongo.Host}:{mongo.Port}";
            }
            return new MongoClient(cs);
        });

        services.AddScoped<IKeeperDomainDirectoryReader, KeeperDomainDirectoryReader>();
        services.AddScoped<IDiscoveryHostStore, MongoDiscoveryHostStore>();
        services.AddScoped<IDiscoveryScanJobStore, MongoDiscoveryScanJobStore>();
        services.AddScoped<IDiscoveryPrefixStore, MongoDiscoveryPrefixStore>();
        services.AddScoped<IEventLogPackageCatalogStore, MongoEventLogPackageCatalogStore>();
        services.AddScoped<IAdComputerDirectoryClient, AdComputerDirectoryClient>();

        return services;
    }
}
