using Microsoft.Extensions.Hosting;
using Quartz.Spi;
using Quartz;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MngEngine.Domain.Entities.Job;
using Quartz.Impl.Matchers;
using System.Threading;

namespace MngEngine.Persistence.Service.HostedService
{
    public class QuartzHostedService : IHostedService
    {
        private readonly ISchedulerFactory _schedulerFactory;
        private readonly IJobFactory _jobFactory;
        private readonly IEnumerable<JobSchedule> _jobSchedules;
        private IScheduler _scheduler;

        public QuartzHostedService(
            ISchedulerFactory schedulerFactory,
            IJobFactory jobFactory,
            IEnumerable<JobSchedule> jobSchedules)
        {
            _schedulerFactory = schedulerFactory;
            _jobFactory = jobFactory;
            _jobSchedules = jobSchedules;
        }

        public async Task UpdateCronSchedule(string jobKey, string newCronExpression)
        {
            IScheduler scheduler = await _schedulerFactory.GetScheduler(new CancellationToken());

            var jobKeys = await scheduler.GetJobKeys(GroupMatcher<JobKey>.AnyGroup());

            var jobKeyObj = new JobKey(jobKey);

            // Mevcut Trigger'ı bul
            var triggers = await scheduler.GetTriggersOfJob(jobKeyObj);

            var aa = await scheduler.GetTriggerKeys(GroupMatcher<TriggerKey>.AnyGroup(), new CancellationToken());

            if (triggers.Any())
            {
                // İlk Trigger'ı al (varsayılan olarak tek trigger olduğundan hareketle)
                var oldTrigger = triggers.First();

                // Yeni Cron Trigger oluştur
                var newTrigger = TriggerBuilder.Create()
                    .WithIdentity(oldTrigger.Key)
                    .WithCronSchedule(newCronExpression)
                    .ForJob(jobKeyObj)
                    .Build();

                // Job'ı yeni trigger ile yeniden planla
                await scheduler.RescheduleJob(oldTrigger.Key, newTrigger);
            }
            else
            {
                // Trigger yoksa yeni bir job tanımlaması yapabilir veya hata yönetimi sağlayabilirsiniz
                throw new Exception($"No triggers found for job {jobKey}");
            }
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _scheduler = await _schedulerFactory.GetScheduler(cancellationToken);
            _scheduler.JobFactory = _jobFactory;

            // Tüm job anahtarlarını almak
            var jobKeys = await _scheduler.GetJobKeys(GroupMatcher<JobKey>.AnyGroup());

            // Tüm jobları bir seferde silmek
            await _scheduler.DeleteJobs(jobKeys.ToList());

            foreach (var jobSchedule in _jobSchedules)
            {


                var job = CreateJob(jobSchedule);
                var trigger = CreateTrigger(jobSchedule);

                await _scheduler.ScheduleJob(job, trigger, cancellationToken);
            }

            await _scheduler.Start(cancellationToken);
        }
        public async Task RestartAsync(CancellationToken cancellationToken)
        {
            await StopAsync(cancellationToken);
            await StartAsync(cancellationToken);
        }
        public async Task StopAsync(CancellationToken cancellationToken)
        {

            if (_scheduler != null)
            {
                await _scheduler.Shutdown(cancellationToken);
            }
        }

        private static IJobDetail CreateJob(JobSchedule schedule)
        {
            var jobType = schedule.JobType;
            return JobBuilder
                .Create(jobType)
                .WithIdentity(jobType.FullName)
                .WithDescription(jobType.Name)
                .Build();
        }

        private static ITrigger CreateTrigger(JobSchedule schedule)
        {
            return TriggerBuilder
                .Create()
                .WithIdentity($"{schedule.JobType.FullName}.trigger")
                .WithCronSchedule(schedule.CronExpression)
                .WithDescription(schedule.CronExpression)
                .Build();
        }

        public async Task RescheduleJobAsync(string jobTypeFullName, string newCronExpression, CancellationToken cancellationToken)
        {
            if (_scheduler == null) throw new InvalidOperationException("Scheduler is not initialized.");

            var cron = ToQuartzCron(newCronExpression);

            var jobKey = new JobKey(jobTypeFullName);
            var triggerKey = new TriggerKey($"{jobTypeFullName}.trigger");

            var newTrigger = TriggerBuilder.Create()
                .WithIdentity(triggerKey)
                .WithCronSchedule(cron)
                .WithDescription(cron)
                .Build();

            await _scheduler.RescheduleJob(triggerKey, newTrigger, cancellationToken);
        }

        /// <summary>
        /// CollectorJob için period gruplarına göre trigger'ları yeniden planlar.
        /// Her (periodExpression, quartzCron) için ayrı trigger oluşturulur; JobDataMap["PeriodExpression"] ile job'a geçer.
        /// Unschedule sonrası job silinebilir (non-durable); bu yüzden trigger eklemeden önce job'u AddJob ile garanti ederiz.
        /// </summary>
        public async Task RescheduleCollectorTriggersAsync(
            IReadOnlyList<(string PeriodExpression, string QuartzCron)> periodTriggers,
            CancellationToken cancellationToken = default)
        {
            if (_scheduler == null) throw new InvalidOperationException("Scheduler is not initialized.");

            const string jobName = "MngEngine.Persistence.Jobs.CollectorJob";
            var jobKey = new JobKey(jobName);
            var collectorSchedule = _jobSchedules.FirstOrDefault(s => s.JobType.FullName == jobName);

            var triggers = await _scheduler.GetTriggersOfJob(jobKey, cancellationToken);
            foreach (var t in triggers)
            {
                await _scheduler.UnscheduleJob(t.Key, cancellationToken);
            }

            var jobDetail = (collectorSchedule != null
                ? JobBuilder.Create(collectorSchedule.JobType).WithIdentity(jobName).WithDescription(collectorSchedule.JobType.Name)
                : JobBuilder.Create<MngEngine.Persistence.Jobs.CollectorJob>().WithIdentity(jobName).WithDescription("CollectorJob"))
                .StoreDurably()
                .Build();
            await _scheduler.AddJob(jobDetail, true, cancellationToken);

            if (periodTriggers.Count == 0)
            {
                var defaultCron = ToQuartzCron("0/15 * * * * ?");
                var defaultTrigger = TriggerBuilder.Create()
                    .WithIdentity($"{jobName}.trigger.default", "DEFAULT")
                    .WithCronSchedule(defaultCron)
                    .ForJob(jobKey)
                    .UsingJobData("PeriodExpression", "")
                    .Build();
                await _scheduler.ScheduleJob(defaultTrigger, cancellationToken);
                return;
            }

            for (var i = 0; i < periodTriggers.Count; i++)
            {
                var (periodExpr, cronExpr) = periodTriggers[i];
                var quartzCron = ToQuartzCron(cronExpr);
                var triggerKey = new TriggerKey($"{jobName}.trigger.p{i}", "DEFAULT");
                var trigger = TriggerBuilder.Create()
                    .WithIdentity(triggerKey)
                    .WithCronSchedule(quartzCron)
                    .ForJob(jobKey)
                    .UsingJobData("PeriodExpression", periodExpr ?? "")
                    .Build();
                await _scheduler.ScheduleJob(trigger, cancellationToken);
            }
        }

        /// <summary>Unix (5 alan) veya eksik cron'u Quartz (6 alan) formatına çevirir. Quartz: saniye dakika saat gün ay haftagünü. ? sadece gün veya haftagünü alanında kullanılabilir.</summary>
        private static string ToQuartzCron(string expression)
        {
            var s = (expression ?? "").Trim();
            if (string.IsNullOrEmpty(s)) return "0 */5 * * * ?";

            var parts = s.Split((char[]?)[' '], StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 5)
            {
                var dow = parts[4] == "*" ? "?" : parts[4];
                return $"0 {parts[0]} {parts[1]} {parts[2]} {parts[3]} {dow}";
            }
            if (parts.Length == 6)
            {
                if (parts[3] == "*" && parts[5] == "*")
                    return $"{parts[0]} {parts[1]} {parts[2]} * {parts[4]} ?";
                return s;
            }
            return "0 */5 * * * ?";
        }
    }
}