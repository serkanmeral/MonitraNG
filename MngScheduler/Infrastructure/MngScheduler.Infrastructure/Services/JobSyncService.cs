using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MngScheduler.Application.Configuration;
using MngScheduler.Application.Constants;
using MngScheduler.Application.Interfaces;
using MngScheduler.Domain.Entities;
using Quartz;
using Quartz.Impl.Matchers;

namespace MngScheduler.Infrastructure.Services;

/// <summary>
/// Background service that synchronizes jobs from MongoDB to Quartz scheduler
/// Supports both polling (30s interval) and immediate sync (via Channel)
/// </summary>
public class JobSyncService : BackgroundService, IJobSyncService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ISchedulerFactory _schedulerFactory;
    private readonly ILogger<JobSyncService> _logger;
    private readonly MngSchedulerSettings _settings;
    private readonly SemaphoreSlim _syncLock = new(1, 1); // Prevent concurrent syncs
    
    // Immediate sync signal channel
    private readonly Channel<bool> _syncChannel = Channel.CreateUnbounded<bool>();

    public JobSyncService(
        IServiceScopeFactory serviceScopeFactory,
        ISchedulerFactory schedulerFactory,
        ILogger<JobSyncService> logger,
        IOptions<MngSchedulerSettings> settings)
    {
        _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
        _schedulerFactory = schedulerFactory ?? throw new ArgumentNullException(nameof(schedulerFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_settings.JobSync.Enabled)
        {
            _logger.LogInformation("JobSyncService is disabled in configuration");
            return;
        }

        _logger.LogInformation("JobSyncService starting...");

        // Initial sync on startup
        try
        {
            await SyncJobsAsync(stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during initial job sync");
        }

        var syncInterval = TimeSpan.FromSeconds(_settings.JobSync.SyncIntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Check for immediate sync signal (non-blocking)
                if (_syncChannel.Reader.TryRead(out _))
                {
                    _logger.LogDebug("Immediate sync triggered");
                    await SyncJobsAsync(stoppingToken);
                    continue; // Skip polling delay
                }

                // Wait for immediate sync signal or polling interval (whichever comes first)
                using (var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken))
                {
                    var delayTask = Task.Delay(syncInterval, cts.Token);
                    var readTask = _syncChannel.Reader.WaitToReadAsync(cts.Token).AsTask();

                    var completedTask = await Task.WhenAny(delayTask, readTask);
                    
                    if (completedTask == readTask && await readTask)
                    {
                        // Immediate sync signal received
                        cts.Cancel(); // Cancel delay
                        _logger.LogDebug("Immediate sync triggered via channel");
                        await SyncJobsAsync(stoppingToken);
                    }
                    else if (completedTask == delayTask)
                    {
                        // Polling interval elapsed
                        _logger.LogDebug("Polling sync triggered");
                        await SyncJobsAsync(stoppingToken);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when stopping
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in JobSyncService main loop");
                // Wait a bit before retrying
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        _logger.LogInformation("JobSyncService stopped");
    }

    /// <summary>
    /// Trigger immediate sync (called from API controllers after job create/update/delete)
    /// </summary>
    public async Task SyncNowAsync()
    {
        try
        {
            await _syncChannel.Writer.WriteAsync(true);
            _logger.LogDebug("Immediate sync signal sent");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send immediate sync signal");
        }
    }

    private async Task SyncJobsAsync(CancellationToken cancellationToken)
    {
        // Prevent concurrent syncs
        if (!await _syncLock.WaitAsync(0, cancellationToken))
        {
            _logger.LogDebug("Sync already in progress, skipping");
            return;
        }

        try
        {
            _logger.LogDebug("Starting job synchronization...");

            var scheduler = await _schedulerFactory.GetScheduler(cancellationToken);

            // Create a scope to access scoped services
            using var scope = _serviceScopeFactory.CreateScope();
            var systemJobRepository = scope.ServiceProvider.GetRequiredService<ISystemJobRepository>();
            var userJobRepository = scope.ServiceProvider.GetRequiredService<IUserJobRepository>();
            var domainLookupService = scope.ServiceProvider.GetRequiredService<IDomainLookupService>();

            // Get active jobs from repositories
            var systemJobs = await systemJobRepository.GetActiveJobsAsync();
            IEnumerable<ScheduledJob> userJobs;
            if (_settings.JobSync.SyncUserJobs)
            {
                string? dgToken = null;
                var oc = _settings.WorkItemScheduleOrchestration;
                var account = oc.ServiceAccount;
                if (!string.IsNullOrWhiteSpace(account.DomainName)
                    && !string.IsNullOrWhiteSpace(account.Username)
                    && !string.IsNullOrWhiteSpace(account.Password))
                {
                    var keeperAuth = scope.ServiceProvider.GetRequiredService<IMngKeeperAuthClient>();
                    var tokenResult = await keeperAuth.GetAccessTokenAsync(cancellationToken);
                    if (tokenResult.Success && !string.IsNullOrWhiteSpace(tokenResult.AccessToken))
                    {
                        dgToken = tokenResult.AccessToken;
                    }
                    else
                    {
                        _logger.LogWarning(
                            "[WorkItemSchedule] JobSync: Keeper token unavailable; user job DG read may fail (HTTP {Status})",
                            tokenResult.HttpStatusCode);
                    }
                }
                else
                {
                    _logger.LogWarning(
                        "[WorkItemSchedule] JobSync: ServiceAccount not configured; user job DG read will fail");
                }

                userJobs = await userJobRepository.GetAllActiveJobsAsync(dgToken);
            }
            else
            {
                userJobs = Enumerable.Empty<ScheduledJob>();
            }

            var allJobs = systemJobs.Concat(userJobs).ToList();
            _logger.LogDebug("Retrieved {SystemCount} system jobs and {UserCount} user jobs (SyncUserJobs={SyncUserJobs})",
                systemJobs.Count(), userJobs.Count(), _settings.JobSync.SyncUserJobs);

            // Get currently scheduled jobs from Quartz
            var scheduledJobKeys = await scheduler.GetJobKeys(GroupMatcher<JobKey>.AnyGroup());
            var scheduledJobsDict = new Dictionary<string, IJobDetail>();

            foreach (var jobKey in scheduledJobKeys)
            {
                var jobDetail = await scheduler.GetJobDetail(jobKey);
                if (jobDetail != null)
                {
                    scheduledJobsDict[jobKey.Name] = jobDetail;
                }
            }

            // Sync jobs
            var jobsToAdd = new List<ScheduledJob>();
            var jobsToUpdate = new List<ScheduledJob>();
            var jobsToRemove = new List<JobKey>();

            foreach (var job in allJobs)
            {
                var jobKey = new JobKey(job.JobId, GetJobGroup(job.JobType));

                if (scheduledJobsDict.ContainsKey(job.JobId))
                {
                    // Job exists, check if update needed
                    var existingJob = scheduledJobsDict[job.JobId];
                    if (await ShouldUpdateJobAsync(scheduler, existingJob, job))
                    {
                        jobsToUpdate.Add(job);
                    }
                }
                else
                {
                    // New job, add it
                    jobsToAdd.Add(job);
                }
            }

            // Find jobs to remove (exist in Quartz but not in repositories)
            foreach (var scheduledJob in scheduledJobsDict)
            {
                if (!allJobs.Any(j => j.JobId == scheduledJob.Key))
                {
                    jobsToRemove.Add(new JobKey(scheduledJob.Key, scheduledJobsDict[scheduledJob.Key].Key.Group));
                }
            }

            // Apply changes
            await AddJobsToSchedulerAsync(scheduler, jobsToAdd, cancellationToken);
            await UpdateJobsInSchedulerAsync(scheduler, jobsToUpdate, cancellationToken);
            await RemoveJobsFromSchedulerAsync(scheduler, jobsToRemove, cancellationToken);

            _logger.LogInformation("Job synchronization completed. Added: {Added}, Updated: {Updated}, Removed: {Removed}",
                jobsToAdd.Count, jobsToUpdate.Count, jobsToRemove.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during job synchronization");
        }
        finally
        {
            _syncLock.Release();
        }
    }

    private async Task AddJobsToSchedulerAsync(IScheduler scheduler, List<ScheduledJob> jobs, CancellationToken cancellationToken)
    {
        foreach (var job in jobs)
        {
            try
            {
                var jobDetail = CreateJobDetail(job);
                var trigger = CreateTrigger(job);

                await scheduler.ScheduleJob(jobDetail, trigger, cancellationToken);
                _logger.LogInformation("Added job to scheduler: {JobId}", job.JobId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to add job to scheduler: {JobId}", job.JobId);
            }
        }
    }

    private async Task UpdateJobsInSchedulerAsync(IScheduler scheduler, List<ScheduledJob> jobs, CancellationToken cancellationToken)
    {
        foreach (var job in jobs)
        {
            try
            {
                var jobKey = new JobKey(job.JobId, GetJobGroup(job.JobType));
                
                // Get existing triggers
                var triggers = await scheduler.GetTriggersOfJob(jobKey);
                
                if (triggers.Any())
                {
                    // Remove old triggers and add new one
                    await scheduler.UnscheduleJobs(triggers.Select(t => t.Key).ToList(), cancellationToken);
                }

                // Update job detail
                var jobDetail = CreateJobDetail(job);
                await scheduler.AddJob(jobDetail, true, cancellationToken); // replace = true

                // Add new trigger
                var trigger = CreateTrigger(job);
                await scheduler.ScheduleJob(trigger, cancellationToken);

                _logger.LogInformation("Updated job in scheduler: {JobId}", job.JobId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update job in scheduler: {JobId}", job.JobId);
            }
        }
    }

    private async Task RemoveJobsFromSchedulerAsync(IScheduler scheduler, List<JobKey> jobKeys, CancellationToken cancellationToken)
    {
        if (jobKeys.Any())
        {
            try
            {
                await scheduler.DeleteJobs(jobKeys, cancellationToken);
                _logger.LogInformation("Removed {Count} jobs from scheduler", jobKeys.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to remove jobs from scheduler");
            }
        }
    }

    private IJobDetail CreateJobDetail(ScheduledJob job)
    {
        var jobDataMap = new JobDataMap
        {
            ["JobId"] = job.JobId,
            ["JobType"] = job.JobType.ToString(),
            ["EndpointUrl"] = job.EndpointUrl,
            ["HttpMethod"] = job.HttpMethod,
            ["TimeoutSeconds"] = job.TimeoutSeconds,
            ["Headers"] = job.Headers != null ? System.Text.Json.JsonSerializer.Serialize(job.Headers) : null,
            ["Payload"] = job.Payload,
            ["DomainId"] = job.DomainId
        };

        var builder = JobBuilder.Create(GetJobTypeForScheduledJob(job))
            .WithIdentity(job.JobId, GetJobGroup(job.JobType))
            .WithDescription(job.Description ?? job.Name)
            .UsingJobData(jobDataMap)
            .StoreDurably();

        return builder.Build();
    }

    private static Type GetJobTypeForScheduledJob(ScheduledJob job)
    {
        if (job.JobType == JobType.System && SystemJobIds.IsDirectorySyncOrchestration(job.JobId))
            return typeof(Jobs.DirectorySyncOrchestrationJob);

        if (job.JobType == JobType.User && UserJobIds.IsWorkItemSchedule(job.JobId))
            return typeof(Jobs.WorkItemScheduleOrchestrationJob);

        return typeof(Jobs.HttpJob);
    }

    private ITrigger CreateTrigger(ScheduledJob job)
    {
        var jobKey = new JobKey(job.JobId, GetJobGroup(job.JobType));
        return TriggerBuilder.Create()
            .WithIdentity($"{job.JobId}_trigger", GetJobGroup(job.JobType))
            .ForJob(jobKey)
            .WithCronSchedule(job.CronExpression)
            .WithDescription($"Trigger for {job.JobId}")
            .Build();
    }

    private string GetJobGroup(JobType jobType)
    {
        return jobType == JobType.System ? "SystemJobs" : "UserJobs";
    }

    private async Task<bool> ShouldUpdateJobAsync(IScheduler scheduler, IJobDetail existingJob, ScheduledJob job)
    {
        var expectedJobType = GetJobTypeForScheduledJob(job);
        if (existingJob.JobType != expectedJobType)
            return true;

        // Get triggers for the job to check cron expression
        var triggers = await scheduler.GetTriggersOfJob(existingJob.Key);
        var cronTrigger = triggers.OfType<ICronTrigger>().FirstOrDefault();
        
        // Check if cron expression changed
        if (cronTrigger != null && cronTrigger.CronExpressionString != job.CronExpression)
        {
            return true;
        }

        if (SystemJobIds.IsDirectorySyncOrchestration(job.JobId))
        {
            var existingOrchestrationHeaders = existingJob.JobDataMap.GetString("Headers");
            var currentOrchestrationHeaders = job.Headers != null ? System.Text.Json.JsonSerializer.Serialize(job.Headers) : null;
            return existingOrchestrationHeaders != currentOrchestrationHeaders;
        }

        // Check if endpoint URL changed
        var existingEndpoint = existingJob.JobDataMap.GetString("EndpointUrl");
        if (existingEndpoint != job.EndpointUrl)
        {
            return true;
        }

        // Check if HTTP method changed
        var existingMethod = existingJob.JobDataMap.GetString("HttpMethod");
        if (existingMethod != job.HttpMethod)
        {
            return true;
        }

        // Check if payload changed
        var existingPayload = existingJob.JobDataMap.GetString("Payload");
        if (existingPayload != job.Payload)
        {
            return true;
        }

        // Check if headers changed
        var existingHeaders = existingJob.JobDataMap.GetString("Headers");
        var currentHeaders = job.Headers != null ? System.Text.Json.JsonSerializer.Serialize(job.Headers) : null;
        if (existingHeaders != currentHeaders)
        {
            return true;
        }

        return false;
    }
}
