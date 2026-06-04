using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using MngReactor.Application.Abstractions.Crypt;
using MngReactor.Application.Abstractions.Data;
using MngReactor.Application.Abstractions.Domain;
using MngReactor.Application.Abstractions.Engine;
using MngReactor.Application.Abstractions.Ingest;
using MngReactor.Application.Abstractions.SecEvents;
using MngReactor.Application.Configuration;
using MngReactor.Application.Repositories.Data;
using MngReactor.Persistence.Repositories.Data;
using MngReactor.Persistence.Services.Crypt;
using MngReactor.Persistence.Services.Data;
using MngReactor.Persistence.Services.Domain;
using MngReactor.Persistence.Services.Engine;
using MngReactor.Persistence.Services.Ingest;
using MngReactor.Persistence.Services.SecEvents;
using MngReactor.Persistence.Services.SecEvents.Parsers;

namespace MngReactor.Persistence
{
    public static class ServiceRegistration
    {
        public static void AddPersistenceServices(this IServiceCollection services)
        {
            services.AddSingleton<IMongoClient>(sp =>
            {
                var opts = sp.GetRequiredService<IOptions<MngReactorSettings>>().Value;
                var mongo = opts.MongoDB;
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

            services.AddHttpClient<DataGatewayClient>();
            services.AddScoped<IDataGatewayClient, DataGatewayClient>();
            services.AddScoped<IMonMetricsRepository, MonMetricsRepository>();
            services.AddScoped<IIngestProcessing, IngestProcessing>();
            services.AddScoped<ISecEventIngestProcessing, SecEventIngestProcessing>();
            services.AddScoped<ISecEventsRepository, SecEventsRepository>();
            services.AddSingleton<WindowsSecurityParser>();
            services.AddSingleton<FirewallGenericSyslogParser>();
            services.AddSingleton<UnknownSecEventFallback>();
            services.AddSingleton<ISecEventParserRegistry, SecEventParserRegistry>();
            services.AddScoped<IDataProcessing, DataProcessing>();
            services.AddScoped<IDataRepository, DataGatewayDataRepository>();
            services.AddScoped<IDomainProcessing, DomainProcessing>();
            services.AddScoped<IDomainDefaultsService, DomainDefaultsProcessing>();
            services.AddScoped<ICryptProcessing, CryptProcessing>();
            services.AddScoped<IEngineProcessing, EngineProcessing>();
            services.AddScoped<IEngineConfigSync, EngineConfigSyncProcessing>();
            services.AddScoped<IConfigStringService, ConfigStringProcessing>();
            services.AddScoped<IEngineStatusProcessing, EngineStatusProcessing>();
            services.AddScoped<IEngineIdsForAssetResolver, EngineIdsForAssetResolver>();
        }
    }
}