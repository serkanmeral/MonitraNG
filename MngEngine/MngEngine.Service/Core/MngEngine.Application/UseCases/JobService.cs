using System;
using Microsoft.Extensions.Caching.Memory;
using MngEngine.Application.Features.EngineConfig;
using MngEngine.Application.Interfaces;
using MngEngine.Domain.Entities.Job;
using Quartz;
using Quartz.Impl.Matchers;
using Serilog;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MngEngine.Application.UseCases
{
    public class JobService : IJobService
    {
        private const string CollectorJobName = "MngEngine.Persistence.Jobs.CollectorJob";

        private readonly ISchedulerFactory _schedulerFactory;
        private readonly IMemoryCache _cache;

        public JobService(ISchedulerFactory schedulerFactory, IMemoryCache cache)
        {
            _schedulerFactory = schedulerFactory;
            _cache = cache;
        }

        public async Task<IEnumerable<JobDetail>> GetJobs()
        {
            try
            {
                var scheduler = await _schedulerFactory.GetScheduler();
                var jobKeys = await scheduler.GetJobKeys(GroupMatcher<JobKey>.AnyGroup());

                var jobs = new List<JobDetail>();
                foreach (var jobKey in jobKeys)
                {
                    var detail = await scheduler.GetJobDetail(jobKey);
                    var jobDetail = new JobDetail
                    {
                        Name = detail.Key.Name,
                        Group = detail.Key.Group,
                        Description = detail.Description
                    };

                    var triggers = await scheduler.GetTriggersOfJob(jobKey);
                    var cronExprs = new List<string>();
                    DateTimeOffset? earliestNext = null;
                    foreach (var trigger in triggers)
                    {
                        if (trigger is ICronTrigger cronTrigger)
                        {
                            cronExprs.Add(cronTrigger.CronExpressionString);
                            var next = trigger.GetNextFireTimeUtc();
                            if (next != null && (earliestNext == null || next < earliestNext))
                                earliestNext = next;
                        }
                    }
                    jobDetail.CronExpression = cronExprs.Count > 0 ? string.Join(" | ", cronExprs.Distinct()) : null;
                    jobDetail.NextFireTimeUtc = earliestNext;

                    if (string.Equals(jobDetail.Name, CollectorJobName, System.StringComparison.Ordinal))
                        jobDetail.Assets = GetCollectorJobAssets();

                    jobs.Add(jobDetail);
                }

                return jobs;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error occurred while getting jobs");
                throw;
            }
        }

        private List<JobAssetSchedule> GetCollectorJobAssets()
        {
            var syncResult = _cache.Get<EngineConfigSyncResult?>("engineConfigSync");
            var configs = syncResult?.AssetConfigs ?? [];
            return configs
                .Select(a => new JobAssetSchedule
                {
                    AssetId = a.AssetId,
                    AssetName = a.AssetName ?? "",
                    AgentName = a.AgentName ?? "",
                    PeriodExpression = a.PeriodExpression
                })
                .ToList();
        }

        public async Task AddJob(JobSchedule jobSchedule)
        {
            try
            {
                var scheduler = await _schedulerFactory.GetScheduler();

                var job = JobBuilder.Create(jobSchedule.JobType)
                    .WithIdentity(jobSchedule.JobType.FullName)
                    .WithDescription(jobSchedule.JobType.Name)
                    .Build();

                var trigger = TriggerBuilder.Create()
                    .WithIdentity($"{jobSchedule.JobType.FullName}.trigger")
                    .WithCronSchedule(jobSchedule.CronExpression)
                    .WithDescription(jobSchedule.CronExpression)
                    .Build();

                await scheduler.ScheduleJob(job, trigger);
                Log.Information("Job {JobName} added successfully", jobSchedule.JobType.Name);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error occurred while adding job");
                throw;
            }
        }

        public async Task DeleteJob(string jobName)
        {
            try
            {
                var scheduler = await _schedulerFactory.GetScheduler();
                var jobKey = new JobKey(jobName);

                await scheduler.DeleteJob(jobKey);
                Log.Information("Job {JobName} deleted successfully", jobName);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error occurred while deleting job");
                throw;
            }
        }

        public async Task<bool> TriggerJobAsync(string jobName)
        {
            try
            {
                var scheduler = await _schedulerFactory.GetScheduler();
                var jobKey = new JobKey(jobName);
                if (await scheduler.GetJobDetail(jobKey) == null)
                    return false;
                await scheduler.TriggerJob(jobKey);
                Log.Information("Job {JobName} triggered manually", jobName);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error triggering job {JobName}", jobName);
                throw;
            }
        }
    }
}