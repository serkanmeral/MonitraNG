using Microsoft.Extensions.DependencyInjection;
using MngScheduler.Application.Configuration;
using MngScheduler.Application.Interfaces;
using MngScheduler.Application.Services;
using MongoDB.Driver;

namespace MngScheduler.Application;

public static class ServiceRegistration
{
    public static void AddApplicationServices(this IServiceCollection collection, MngSchedulerSettings mngSchedulerSettings)
    {
        collection.Configure<MngSchedulerSettings>(_ =>
        {
            _.MongoDB = mngSchedulerSettings.MongoDB;
            _.RabbitMQ = mngSchedulerSettings.RabbitMQ;
            _.CertificateSettings = mngSchedulerSettings.CertificateSettings;
            _.OpenApiServerPath = mngSchedulerSettings.OpenApiServerPath;
            _.Actors = mngSchedulerSettings.Actors;
            _.DataGateway = mngSchedulerSettings.DataGateway;
            _.JobSync = mngSchedulerSettings.JobSync;
            _.Quartz = mngSchedulerSettings.Quartz;
            _.HttpClient = mngSchedulerSettings.HttpClient;
            _.DirectorySyncOrchestration = mngSchedulerSettings.DirectorySyncOrchestration;
            _.WorkItemScheduleOrchestration = mngSchedulerSettings.WorkItemScheduleOrchestration;
        });

        // Add MongoDB
        collection.AddSingleton<IMongoClient>(provider =>
        {
            var connectionString = mngSchedulerSettings.MongoDB.ConnectionString ?? "mongodb://localhost:27017";
            
            // MongoDB conventions for DateTime serialization
            var conventionPack = new MongoDB.Bson.Serialization.Conventions.ConventionPack
            {
                new MongoDB.Bson.Serialization.Conventions.StringObjectIdIdGeneratorConvention()
            };
            MongoDB.Bson.Serialization.Conventions.ConventionRegistry.Register(
                "MngSchedulerConventions",
                conventionPack,
                t => true);
            
            return new MongoClient(connectionString);
        });

        collection.AddMediatR(config =>
        {
            config.RegisterServicesFromAssemblies(AppDomain.CurrentDomain.GetAssemblies());
        });

        // Application Services
        collection.AddScoped<ISystemJobService, SystemJobService>();
        collection.AddScoped<IUserJobService, UserJobService>();
    }
}
