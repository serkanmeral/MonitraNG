using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MngWorkflow.Application.Configuration;
using MngWorkflow.Application.Contracts;
using MngWorkflow.Application.Repositories;
using MngWorkflow.Application.Services;
using MngWorkflow.Domain.Constants;
using MngWorkflow.Domain.Entities;
using MngWorkflow.Domain.Enums;
using MngWorkflow.Infrastructure.Utilities;

namespace MngWorkflow.Infrastructure.Services;

public sealed class WorkflowScheduleSyncService : IWorkflowScheduleSyncService
{
    private readonly IWorkflowSchedulerClient _scheduler;
    private readonly IWorkflowKeeperAuthClient _keeperAuth;
    private readonly SchedulerSettings _settings;
    private readonly ILogger<WorkflowScheduleSyncService> _logger;

    public WorkflowScheduleSyncService(
        IWorkflowSchedulerClient scheduler,
        IWorkflowKeeperAuthClient keeperAuth,
        IOptions<MngWorkflowSettings> settings,
        ILogger<WorkflowScheduleSyncService> logger)
    {
        _scheduler = scheduler;
        _keeperAuth = keeperAuth;
        _settings = settings.Value.Scheduler;
        _logger = logger;
    }

    public async Task SyncPublishedVersionAsync(WorkflowVersionDocument version, CancellationToken cancellationToken = default)
    {
        if (version.Status != WorkflowVersionStatus.Published)
            return;

        var scheduleTriggers = version.Triggers
            .Where(t => t.Enabled && t.Type == WorkflowTriggerTypes.Schedule)
            .ToList();

        var token = await _keeperAuth.GetServiceAccessTokenAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(token))
        {
            if (scheduleTriggers.Count > 0)
                _logger.LogWarning("Schedule triggers present but service account missing; workflow={WorkflowId}", version.WorkflowId);
            return;
        }

        var jobId = $"{_settings.JobIdPrefix}schedule-{version.WorkflowId}";

        if (scheduleTriggers.Count == 0)
        {
            await TryDeleteJobAsync(jobId, token, cancellationToken);
            return;
        }

        var trigger = scheduleTriggers[0];
        var cron = ResolveCronExpression(trigger);
        if (string.IsNullOrWhiteSpace(cron))
        {
            _logger.LogWarning("Schedule trigger missing cronExpression workflow={WorkflowId}", version.WorkflowId);
            return;
        }

        var endpointUrl = ResolveHookUrl(_settings.ScheduleRunPath);
        var payload = JsonSerializer.Serialize(new WorkflowScheduleRunRequest
        {
            WorkflowId = version.WorkflowId,
            WorkflowVersionId = version.Id,
            DomainName = version.DomainName,
            DomainId = version.DomainId
        });

        var job = new WorkflowSchedulerUserJobDto
        {
            JobId = jobId,
            JobType = 1,
            Name = $"Workflow schedule {version.WorkflowId}",
            Description = $"Schedule trigger for workflow {version.WorkflowId} v{version.Version}",
            CronExpression = cron,
            EndpointUrl = endpointUrl,
            HttpMethod = "POST",
            Payload = payload,
            IsActive = true,
            TimeoutSeconds = 300
        };

        var existing = await _scheduler.GetUserJobAsync(jobId, token, cancellationToken);
        if (existing == null)
            await _scheduler.CreateUserJobAsync(job, token, cancellationToken);
        else
            await _scheduler.UpdateUserJobAsync(job, token, cancellationToken);

        _logger.LogInformation("Synced schedule trigger workflow={WorkflowId} job={JobId} cron={Cron}", version.WorkflowId, jobId, cron);
    }

    public Task RemoveForWorkflowAsync(string domainName, string workflowId, CancellationToken cancellationToken = default) =>
        TryDeleteJobAsync($"{_settings.JobIdPrefix}schedule-{workflowId}", cancellationToken);

    private async Task TryDeleteJobAsync(string jobId, CancellationToken cancellationToken)
    {
        var token = await _keeperAuth.GetServiceAccessTokenAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(token))
            return;

        await TryDeleteJobAsync(jobId, token, cancellationToken);
    }

    private async Task TryDeleteJobAsync(string jobId, string token, CancellationToken cancellationToken)
    {
        try
        {
            await _scheduler.DeleteUserJobAsync(jobId, token, cancellationToken);
            _logger.LogInformation("Deleted scheduler job {JobId}", jobId);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Scheduler job delete skipped job={JobId}", jobId);
        }
    }

    private static string? ResolveCronExpression(WorkflowTriggerDefinition trigger)
    {
        if (trigger.Config.TryGetValue("cronExpression", out var raw) && raw != null)
            return raw.ToString()?.Trim();

        return null;
    }

    private string ResolveHookUrl(string path)
    {
        if (path.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return path;

        var baseUrl = _settings.HookBaseUrl?.Trim().TrimEnd('/');
        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new InvalidOperationException("Scheduler HookBaseUrl is not configured.");

        return baseUrl + (path.StartsWith('/') ? path : "/" + path);
    }
}
