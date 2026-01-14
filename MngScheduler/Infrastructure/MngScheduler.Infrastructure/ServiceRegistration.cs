using Microsoft.Extensions.DependencyInjection;
using MngScheduler.Application.Configuration;
using MngScheduler.Application.Interfaces;
using MngScheduler.Infrastructure.Clients;
using MngScheduler.Infrastructure.Jobs;
using MngScheduler.Infrastructure.Services;
using Quartz;

namespace MngScheduler.Infrastructure;

public static class ServiceRegistration
{
    public static void AddInfrastructureServices(this IServiceCollection services, MngSchedulerSettings settings)
    {
        // MngDataGateway HttpClient
        services.AddHttpClient("MngDataGateway", client =>
        {
            // Base URL will be set in MngDataGatewayClient constructor
            client.Timeout = TimeSpan.FromSeconds(300);
        });

        // HttpJob HttpClient
        services.AddHttpClient("HttpJob", client =>
        {
            // Timeout will be set per job in HttpJob
            client.Timeout = TimeSpan.FromSeconds(300); // Default timeout
        });

        // MngDataGateway Client
        services.AddScoped<IMngDataGatewayClient, MngDataGatewayClient>();

        // HttpJob registration (Quartz will use this)
        services.AddScoped<HttpJob>();

        // RabbitMQ Event Publisher
        services.AddSingleton<IRabbitMqEventPublisher, RabbitMqEventPublisher>();

        // Quartz.NET Configuration
        services.AddQuartz(q =>
        {
            q.UseMicrosoftDependencyInjectionJobFactory();
            q.UseSimpleTypeLoader();
            q.UseInMemoryStore();
            q.UseDefaultThreadPool(tp =>
            {
                tp.MaxConcurrency = settings.Quartz.ThreadPool.ThreadCount;
            });

            // Scheduler name
            q.SchedulerName = settings.Quartz.SchedulerName;
        });

        // Quartz Hosted Service
        services.AddQuartzHostedService(options =>
        {
            options.WaitForJobsToComplete = true;
            options.AwaitApplicationStarted = true;
        });

        // JobSyncService (BackgroundService + Interface for API access)
        services.AddSingleton<JobSyncService>();
        services.AddHostedService(sp => sp.GetRequiredService<JobSyncService>());
        services.AddSingleton<MngScheduler.Application.Interfaces.IJobSyncService>(sp => sp.GetRequiredService<JobSyncService>());
    }
}
