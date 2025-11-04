using MediatR;
using Microsoft.Extensions.DependencyInjection;
using MngKeeper.Application.Configuration;
using MongoDB.Driver;
using System.Reflection;

namespace MngKeeper.Application;

public static class ServiceRegistration
{
    public static void AddApplicationServices(this IServiceCollection services, MngKeeperSettings settings)
    {
        // Configure Settings
        services.Configure<MngKeeperSettings>(_ =>
        {
            _.MongoDB = settings.MongoDB;
            _.RabbitMQ = settings.RabbitMQ;
            _.Redis = settings.Redis;
            _.Mqtt = settings.Mqtt;
            _.Keycloak = settings.Keycloak;
            _.CertificateSettings = settings.CertificateSettings;
            _.OpenApiServerPath = settings.OpenApiServerPath;
        });

        // Add MediatR
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(
            typeof(Features.Domain.Commands.CreateDomain.CreateDomainCommand).Assembly));

        // Add MongoDB
        services.AddSingleton<IMongoClient>(provider =>
        {
            var connectionString = settings.MongoDB.ConnectionString ?? "mongodb://localhost:27017";
            return new MongoClient(connectionString);
        });

        services.AddSingleton<IMongoDatabase>(provider =>
        {
            var client = provider.GetRequiredService<IMongoClient>();
            var databaseName = settings.MongoDB.DatabaseName ?? "MngKeeper";
            return client.GetDatabase(databaseName);
        });
    }
}

