using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using MngScheduler.Application.Constants;
using MngScheduler.Application.Interfaces;
using MngScheduler.Domain.Entities;
using MngScheduler.Domain.Exceptions;
using Quartz;

namespace MngScheduler.Infrastructure.Jobs;

/// <summary>
/// Operation Core <c>oc-sla-scan-*</c> user job: Keeper token → MO scan-breaches.
/// </summary>
[DisallowConcurrentExecution]
public class SlaBreachScanOrchestrationJob : IJob
{
    private readonly ISlaBreachScanOrchestrationService _orchestrationService;
    private readonly IMngKeeperAuthClient _keeperAuth;
    private readonly IUserJobRepository _userJobRepository;
    private readonly IJobExecutionRepository _executionRepository;
    private readonly IRabbitMqEventPublisher _eventPublisher;
    private readonly ILogger<SlaBreachScanOrchestrationJob> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public SlaBreachScanOrchestrationJob(
        ISlaBreachScanOrchestrationService orchestrationService,
        IMngKeeperAuthClient keeperAuth,
        IUserJobRepository userJobRepository,
        IJobExecutionRepository executionRepository,
        IRabbitMqEventPublisher eventPublisher,
        ILogger<SlaBreachScanOrchestrationJob> logger)
    {
        _orchestrationService = orchestrationService;
        _keeperAuth = keeperAuth;
        _userJobRepository = userJobRepository;
        _executionRepository = executionRepository;
        _eventPublisher = eventPublisher;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var executionId = Guid.NewGuid().ToString();
        var jobId = context.JobDetail.Key.Name;
        var domainId = context.JobDetail.JobDataMap.GetString("DomainId");
        var workspaceId = UserJobIds.TryGetSlaBreachScanWorkspaceId(jobId);
        var stopwatch = Stopwatch.StartNew();

        _logger.LogInformation(
            "[SlaBreachScan] Quartz job started jobId={JobId} workspaceId={WorkspaceId} executionId={ExecutionId}",
            jobId,
            workspaceId,
            executionId);

        if (string.IsNullOrWhiteSpace(workspaceId))
            throw new Quartz.JobExecutionException($"Invalid OC SLA scan job id: {jobId}");

        if (string.IsNullOrWhiteSpace(domainId))
            throw new Quartz.JobExecutionException($"DomainId missing for user job {jobId}");

        var serviceToken = await GetServiceTokenAsync(context.CancellationToken);
        ScheduledJob? job = null;

        try
        {
            job = await _userJobRepository.GetJobByIdAsync(domainId, jobId, serviceToken);
            if (job == null)
                throw new InvalidOperationException($"User job {jobId} not found in domain {domainId}");

            if (!job.ShouldExecute())
            {
                _logger.LogWarning("[SlaBreachScan] Job {JobId} should not execute, skipping", jobId);
                return;
            }

            var result = await _orchestrationService.ScanWorkspaceAsync(workspaceId, context.CancellationToken);
            stopwatch.Stop();

            var status = result.IsSuccess ? "success" : "failed";
            var responseBody = result.ResponseBody;
            if (string.IsNullOrWhiteSpace(responseBody) && !string.IsNullOrWhiteSpace(result.ErrorMessage))
            {
                responseBody = JsonSerializer.Serialize(new
                {
                    error = result.ErrorMessage,
                    result.ResponseBreachesProcessed,
                    result.ResolveBreachesProcessed
                }, JsonOptions);
            }

            var execution = new JobExecution
            {
                ExecutionId = executionId,
                JobId = jobId,
                Status = status,
                ExecutedAt = DateTime.UtcNow,
                ResponseTimeMs = stopwatch.ElapsedMilliseconds,
                ResponseCode = result.HttpStatusCode,
                ResponseBody = responseBody,
                ErrorMessage = result.IsSuccess ? null : result.ErrorMessage,
                DomainId = domainId
            };

            await _executionRepository.SaveUserJobExecutionAsync(domainId, execution, serviceToken);

            if (status == "success")
                job.IncrementSuccessfulExecutionCount();
            else
                job.IncrementFailedExecutionCount();

            job.CheckExecutionLimit();
            job.LastExecution = execution;
            job.UpdatedAt = DateTime.UtcNow;

            try
            {
                await _userJobRepository.UpdateJobAsync(domainId, job, serviceToken);
            }
            catch (JobNotFoundException)
            {
                _logger.LogWarning("[SlaBreachScan] Job {JobId} deleted during execution", jobId);
            }

            try
            {
                await _eventPublisher.PublishJobExecutionCompletedAsync(execution, job);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[SlaBreachScan] Event publish failed jobId={JobId}", jobId);
            }

            if (!result.IsSuccess)
                throw new Quartz.JobExecutionException($"SLA breach scan failed: {result.ErrorMessage}");
        }
        catch (Exception ex) when (ex is not Quartz.JobExecutionException)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "[SlaBreachScan] Job failed jobId={JobId}", jobId);

            var execution = new JobExecution
            {
                ExecutionId = executionId,
                JobId = jobId,
                Status = "failed",
                ExecutedAt = DateTime.UtcNow,
                ResponseTimeMs = stopwatch.ElapsedMilliseconds,
                ErrorMessage = ex.Message,
                DomainId = domainId
            };

            try
            {
                await _executionRepository.SaveUserJobExecutionAsync(domainId, execution, serviceToken);
            }
            catch (Exception saveEx)
            {
                _logger.LogError(saveEx, "[SlaBreachScan] Failed to save execution jobId={JobId}", jobId);
            }

            if (job != null)
            {
                try
                {
                    job.IncrementFailedExecutionCount();
                    job.LastExecution = execution;
                    job.UpdatedAt = DateTime.UtcNow;
                    await _userJobRepository.UpdateJobAsync(domainId, job, serviceToken);
                }
                catch (JobNotFoundException)
                {
                    // ignored
                }
            }

            throw;
        }
    }

    private async Task<string?> GetServiceTokenAsync(CancellationToken cancellationToken)
    {
        var tokenResult = await _keeperAuth.GetAccessTokenAsync(cancellationToken);
        return tokenResult.Success ? tokenResult.AccessToken : null;
    }
}
