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
            });

            // Add MongoDB
            collection.AddSingleton<IMongoClient>(provider =>
            {
                var connectionString = mngDataGatewaySettings.MongoDB.ConnectionString ?? "mongodb://localhost:27017";
                return new MongoClient(connectionString);
            });

            collection.AddMediatR(config =>
            {
                config.RegisterServicesFromAssemblies(AppDomain.CurrentDomain.GetAssemblies());
            });
        }
    }
}
