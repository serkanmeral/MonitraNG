using Microsoft.Extensions.DependencyInjection;
using MngAdmin.Application.Configuration;
using MongoDB.Driver;

namespace MngAdmin.Application;

public static class ServiceRegistration
{
    public static void AddApplicationServices(this IServiceCollection collection, MngAdminSettings mngAdminSettings)
    {
        collection.Configure<MngAdminSettings>(_ =>
        {
            _.MongoDB = mngAdminSettings.MongoDB;
            _.RabbitMQ = mngAdminSettings.RabbitMQ;
            _.CertificateSettings = mngAdminSettings.CertificateSettings;
            _.OpenApiServerPath = mngAdminSettings.OpenApiServerPath;
            _.Actors = mngAdminSettings.Actors;
            _.Backup = mngAdminSettings.Backup;
            _.MinIO = mngAdminSettings.MinIO;
        });

        // Add MongoDB
        collection.AddSingleton<IMongoClient>(provider =>
        {
            var connectionString = mngAdminSettings.MongoDB.ConnectionString ?? "mongodb://localhost:27017";
            
            // MongoDB conventions for DateTime serialization
            var conventionPack = new MongoDB.Bson.Serialization.Conventions.ConventionPack
            {
                new MongoDB.Bson.Serialization.Conventions.StringObjectIdIdGeneratorConvention()
            };
            MongoDB.Bson.Serialization.Conventions.ConventionRegistry.Register(
                "MngAdminConventions",
                conventionPack,
                t => true);
            
            return new MongoClient(connectionString);
        });

        collection.AddMediatR(config =>
        {
            config.RegisterServicesFromAssemblies(AppDomain.CurrentDomain.GetAssemblies());
        });
    }
}
