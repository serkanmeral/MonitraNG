using Microsoft.Extensions.DependencyInjection;
using MngDataGateway.Application.Configuration;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MngDataGateway.Application
{
    public static class ServiceRegistration
    {
        public static void AddApplicationServices(this IServiceCollection collection, MngDataGatewaySettings mngDataGatewaySettings)
        {
            collection.Configure<MngDataGatewaySettings>(_ =>
            {
                _.MongoDB = mngDataGatewaySettings.MongoDB;
                _.RabbitMQ = mngDataGatewaySettings.RabbitMQ;
                _.CertificateSettings = mngDataGatewaySettings.CertificateSettings;
                _.OpenApiServerPath = mngDataGatewaySettings .OpenApiServerPath;
                _.Actors = mngDataGatewaySettings.Actors;
                _.FileStorage = mngDataGatewaySettings.FileStorage;
            });

            // Add MongoDB
            collection.AddSingleton<IMongoClient>(provider =>
            {
                var connectionString = mngDataGatewaySettings.MongoDB.ConnectionString ?? "mongodb://localhost:27017";
                
                // MongoDB conventions for DateTime serialization
                var conventionPack = new MongoDB.Bson.Serialization.Conventions.ConventionPack
                {
                    new MongoDB.Bson.Serialization.Conventions.StringObjectIdIdGeneratorConvention()
                };
                MongoDB.Bson.Serialization.Conventions.ConventionRegistry.Register(
                    "MngDataGatewayConventions",
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
}
