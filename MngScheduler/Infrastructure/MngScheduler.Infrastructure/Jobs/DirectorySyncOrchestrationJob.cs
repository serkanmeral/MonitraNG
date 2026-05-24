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
/// K3 — Quartz job for <see cref="SystemJobIds.DirectorySyncAllDomains"/>.
/// </summary>
[DisallowConcurrentExecution]
public class DirectorySyncOrchestrationJob : IJob
{
    private readonly IDirectorySyncOrchestrationService _orchestrationService;
    private readonly ISystemJobRepository _systemJobRepository;
    private readonly IJobExecutionRepository _executionRepository;
    private readonly IRabbitMqEventPublisher _eventPublisher;
    private readonly ILogger<DirectorySyncOrchestrationJob> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public DirectorySyncOrchestrationJob(
        IDirectorySyncOrchestrationService orchestrationService,
        ISystemJobRepository systemJobRepository,
        IJobExecutionRepository executionRepository,
        IRabbitMqEventPublisher eventPublisher,
        ILogger<DirectorySyncOrchestrationJob> logger)
    {
        _orchestrationService = orchestrationService;
        _systemJobRepository = systemJobRepository;
        _executionRepository = executionRepository;
        _eventPublisher = eventPublisher;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var executionId = Guid.NewGuid().ToString();
        var jobId = context.JobDetail.Key.Name;
        var stopwatch = Stopwatch.StartNew();

        _logger.LogInformation(
            "[DirectorySync] Quartz job started jobId={JobId} executionId={ExecutionId} fireTimeUtc={FireTimeUtc}",
            jobId, executionId, context.FireTimeUtc);

        ScheduledJob? job = null;
        try
        {
            job = await _systemJobRepository.GetJobByIdAsync(jobId);
            if (job == null)
                throw new InvalidOperationException($"System job {jobId} not found");

            if (!job.ShouldExecute())
            {
                _logger.LogWarning("Job {JobId} should not execute, skipping", jobId);
                return;
            }

            var headers = ParseHeaders(context.JobDetail.JobDataMap.GetString("Headers"));
            var orchestrationResult = await _orchestrationService.RunAsync(headers, context.CancellationToken);
            stopwatch.Stop();

            var status = orchestrationResult.IsSuccess ? "success" : "failed";
            var responseBody = JsonSerializer.Serialize(orchestrationResult, JsonOptions);
            if (responseBody.Length > 10240)
                responseBody = responseBody[..10240] + "... [truncated]";

            var execution = new JobExecution
            {
                ExecutionId = executionId,
                JobId = jobId,
                Status = status,
                ExecutedAt = DateTime.UtcNow,
                ResponseTimeMs = stopwatch.ElapsedMilliseconds,
                ResponseCode = orchestrationResult.IsSuccess ? 200 : 500,
                ResponseBody = responseBody,
                ErrorMessage = orchestrationResult.IsSuccess ? null : orchestrationResult.Summary
            };

            await _executionRepository.SaveSystemJobExecutionAsync(execution);

            if (status == "success")
                job.IncrementSuccessfulExecutionCount();
            else
                job.IncrementFailedExecutionCount();

            job.CheckExecutionLimit();
            job.LastExecution = execution;
            job.UpdatedAt = DateTime.UtcNow;

            try
            {
                await _systemJobRepository.UpdateJobAsync(job);
            }
            catch (JobNotFoundException)
            {
                _logger.LogWarning("Job {JobId} was deleted during execution", jobId);
            }

            try
            {
                await _eventPublisher.PublishJobExecutionCompletedAsync(execution, job);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to publish job execution event for {JobId}", jobId);
            }

            if (!orchestrationResult.IsSuccess)
                throw new Quartz.JobExecutionException($"Directory sync orchestration had failures: {orchestrationResult.Summary}");
        }
        catch (Exception ex) when (ex is not Quartz.JobExecutionException)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Directory sync orchestration job failed: {JobId}", jobId);

            var execution = new JobExecution
            {
                ExecutionId = executionId,
                JobId = jobId,
                Status = "failed",
                ExecutedAt = DateTime.UtcNow,
                ResponseTimeMs = stopwatch.ElapsedMilliseconds,
                ErrorMessage = ex.Message
            };

            try
            {
                await _executionRepository.SaveSystemJobExecutionAsync(execution);
            }
            catch (Exception saveEx)
            {
                _logger.LogError(saveEx, "Failed to save execution for job {JobId}", jobId);
            }

            if (job != null)
            {
                try
                {
                    job.IncrementFailedExecutionCount();
                    job.LastExecution = execution;
                    job.UpdatedAt = DateTime.UtcNow;
                    await _systemJobRepository.UpdateJobAsync(job);
                }
                catch (JobNotFoundException)
                {
                    // ignored
                }
            }

            throw;
        }
    }

    private static Dictionary<string, string>? ParseHeaders(string? headersJson)
    {
        if (string.IsNullOrWhiteSpace(headersJson))
            return null;

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(headersJson);
        }
        catch
        {
            return null;
        }
    }
}
