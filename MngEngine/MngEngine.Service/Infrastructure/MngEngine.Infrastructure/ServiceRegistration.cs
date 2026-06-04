using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MngEngine.Application.Interfaces;
using MngEngine.Infrastructure.Context;
using MngEngine.Infrastructure.Service;

namespace MngEngine.Infrastructure
{
    public static class ServiceRegistration
    {
        public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<MqttEngineOptions>(configuration.GetSection(MqttEngineOptions.SectionName));

            services.AddSingleton<IRestContext, RestContext>();
            services.AddSingleton<IAccessTokenProvider, AccessTokenProvider>();
            services.AddSingleton<IIngestClient, IngestClient>();
            services.AddSingleton<IConfigSyncClient, ConfigSyncClient>();
            services.AddSingleton<IEngineStatusClient, EngineStatusClient>();
            services.AddSingleton<IMqttEngineSubscriber, MqttEngineSubscriber>();

            return services;
        }
    }
}