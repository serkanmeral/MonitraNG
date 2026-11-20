using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MngExtensions.Application.Configuration;
using MngExtensions.Application.Pdks;
using MngExtensions.Infrastructure.Pdks;
using StackExchange.Redis;

namespace MngExtensions.Infrastructure;

public static class ServiceRegistration
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
    {
        services.AddHttpClient("MngKeeper", client => client.Timeout = TimeSpan.FromSeconds(15));
        services.AddHttpClient("MngDataGateway", client => client.Timeout = TimeSpan.FromSeconds(30));

        services.AddSingleton<IConnectionMultiplexer>(sp =>
        {
            var settings = sp.GetRequiredService<IOptions<MngExtensionsSettings>>().Value.Redis;
            var options = new ConfigurationOptions
            {
                AbortOnConnectFail = false,
                ConnectTimeout = 2000,
                Password = string.IsNullOrWhiteSpace(settings.Password) ? null : settings.Password
            };
            options.EndPoints.Add(settings.Host, settings.Port);
            return ConnectionMultiplexer.Connect(options);
        });

        services.AddSingleton<IPasscodeStore, RedisPasscodeStore>();
        services.AddSingleton<IKeeperTokenSource, KeeperTokenSource>();
        services.AddSingleton<IPdksDataGateway, PdksDataGateway>();
        services.AddScoped<IPdksService, PdksService>();
        services.AddHostedService<PassagePhotoRetentionService>();
        return services;
    }
}
