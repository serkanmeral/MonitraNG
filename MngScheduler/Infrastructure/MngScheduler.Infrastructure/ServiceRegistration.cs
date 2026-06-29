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

        services.AddHttpClient("MngKeeperDirectorySync", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(settings.HttpClient.TimeoutSeconds);
        });

        services.AddHttpClient("MngKeeperAuth", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(Math.Min(settings.HttpClient.TimeoutSeconds, 60));
        });

        services.AddHttpClient("WorkItemScheduleExecute", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(Math.Max(settings.HttpClient.TimeoutSeconds, 120));
        });

        // MngDataGateway Client
        services.AddScoped<IMngDataGatewayClient, MngDataGatewayClient>();

        // Quartz jobs
        services.AddScoped<HttpJob>();
        services.AddScoped<DirectorySyncOrchestrationJob>();
        services.AddScoped<WorkItemScheduleOrchestrationJob>();
        services.AddScoped<SlaBreachScanOrchestrationJob>();
        services.AddScoped<AlarmValidationOrchestrationJob>();

        services.AddScoped<IMngKeeperDirectorySyncClient, MngKeeperDirectorySyncClient>();
        services.AddScoped<IDirectorySyncOrchestrationService, DirectorySyncOrchestrationService>();
        // Singleton: in-memory Keeper token cache shared across JobSync polls and Quartz jobs.
        services.AddSingleton<IMngKeeperAuthClient, MngKeeperAuthClient>();
        services.AddScoped<IWorkItemScheduleOrchestrationService, WorkItemScheduleOrchestrationService>();
        services.AddScoped<ISlaBreachScanOrchestrationService, SlaBreachScanOrchestrationService>();
        services.AddScoped<IAlarmValidationOrchestrationService, AlarmValidationOrchestrationService>();

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
