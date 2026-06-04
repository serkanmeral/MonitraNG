using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using MngEngine.Application.Interfaces;
using MngEngine.Persistence.Options;
using MngEngine.Persistence.Service.Queue;
using MngEngine.Application.UseCases;
using MngEngine.Domain.Entities.Job;
using MngEngine.Persistence.Factory.JobFactory;
using MngEngine.Persistence.Jobs;
using MngEngine.Persistence.Service.Asset;
using MngEngine.Persistence.Service.Engine;
using MngEngine.Persistence.Service.Config;
using MngEngine.Persistence.Service.Crypt;
using MngEngine.Persistence.Service.HostedService;
using MngEngine.Persistence.Service.Init;
using MngEngine.Persistence.Service.SecEvents;
using Quartz;
using Quartz.Impl;
using Quartz.Spi;

namespace MngEngine.Persistence
{
    public static class ServiceRegistration
    {
        public static IServiceCollection AddPersistenceServices(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<QueueOptions>(configuration.GetSection(QueueOptions.SectionName));
            services.Configure<EngineStatusJobOptions>(configuration.GetSection(EngineStatusJobOptions.SectionName));
            services.Configure<SecEventFixtureOptions>(configuration.GetSection(SecEventFixtureOptions.SectionName));
            services.AddSingleton<IConfigService, ConfigService>();
            services.AddSingleton<IEngineConfigProvider, EngineConfigProvider>();
            services.AddSingleton<IJobRescheduleService, JobRescheduleService>();
            services.AddSingleton<IInitApplicationService, InitApplicationService>();
            services.AddSingleton<ICryptProcessing, CryptProcessing>();
            services.AddSingleton<IAssetService, AssetService>();
            services.AddSingleton<IMetricBatchQueue, MetricBatchQueue>();
            services.AddSingleton<ISecEventFixtureReplay, SecEventFixtureReplayService>();
            services.AddSingleton<MqttSyncTriggerService>();

            #region Quartz
            services.AddSingleton<IJobFactory, SingletonJobFactory>();
            services.AddSingleton<ISchedulerFactory, StdSchedulerFactory>();

            services.AddSingleton<IEngineErrorBuffer, EngineErrorBuffer>();
            services.AddSingleton<CollectorJob>();
            services.AddSingleton<SendJob>();
            services.AddSingleton<ConfigSyncJob>();
            services.AddSingleton<EngineStatusJob>();

            services.AddSingleton<IEnumerable<JobSchedule>>(sp =>
            {
                var config = sp.GetRequiredService<IConfiguration>();
                var statusCron = config.GetValue<string>("MngEngine:EngineStatusJob:CronExpression") ?? "0 */2 * * * ?";
                return new[]
                {
                    new JobSchedule(typeof(CollectorJob), "0/10 * * * * ?"),
                    new JobSchedule(typeof(SendJob), "0 */2 * * * ?"), // Her 2 dakikada
                    new JobSchedule(typeof(ConfigSyncJob), "0 */10 * * * ?"), // Her 10 dakikada
                    new JobSchedule(typeof(EngineStatusJob), statusCron) // Status heartbeat – appsettings / env ile
                };
            });

            services.AddSingleton<MngEngine.Persistence.Service.HostedService.QuartzHostedService>();

            services.AddQuartz(q => q.UseMicrosoftDependencyInjectionJobFactory());
            services.AddQuartzHostedService(options => options.WaitForJobsToComplete = true);

            services.AddSingleton<IJobService, JobService>();
            #endregion

            return services;
        }
    }
}