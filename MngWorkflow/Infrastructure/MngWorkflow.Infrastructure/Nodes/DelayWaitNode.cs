using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MngWorkflow.Application.Configuration;
using MngWorkflow.Application.Contracts;
using MngWorkflow.Application.Execution;
using MngWorkflow.Application.Nodes;
using MngWorkflow.Application.Services;
using MngWorkflow.Domain.Constants;
using MngWorkflow.Domain.Entities;
using MngWorkflow.Infrastructure.Utilities;

namespace MngWorkflow.Infrastructure.Nodes;

public sealed class DelayWaitNode(IServiceScopeFactory scopeFactory) : IWorkflowNode
{
    public string NodeType => WorkflowNodeTypes.DelayWait;

    public async Task<NodeExecutionResult> ExecuteAsync(
        WorkflowExecutionContext context,
        WorkflowNodeDefinition node,
        CancellationToken cancellationToken)
    {
        var delaySeconds = ResolveDelaySeconds(node);
        if (delaySeconds <= 0)
            return NodeExecutionResult.Fail("delay.wait: delaySeconds must be a positive integer", retryable: false);

        using var scope = scopeFactory.CreateScope();
        var settings = scope.ServiceProvider.GetRequiredService<IOptions<MngWorkflowSettings>>().Value;
        var publisher = scope.ServiceProvider.GetRequiredService<IWorkflowQueuePublisher>();
        var scheduler = scope.ServiceProvider.GetRequiredService<IWorkflowSchedulerClient>();
        var keeperAuth = scope.ServiceProvider.GetRequiredService<IWorkflowKeeperAuthClient>();

        var resumeAt = DateTime.UtcNow.AddSeconds(delaySeconds);
        var output = new Dictionary<string, object?>
        {
            ["delaySeconds"] = delaySeconds,
            ["resumeAt"] = resumeAt.ToString("O"),
            ["strategy"] = delaySeconds <= settings.Engine.DelaySchedulerThresholdSeconds ? "bucket" : "scheduler"
        };

        var resumeMessage = new WorkflowResumeMessage
        {
            InstanceId = context.InstanceId,
            WorkflowVersionId = context.WorkflowVersionId,
            NodeId = node.Id,
            EdgeKey = "default",
            CorrelationId = context.CorrelationId,
            DomainId = context.DomainId,
            DomainName = context.DomainName
        };

        if (delaySeconds <= settings.Engine.DelaySchedulerThresholdSeconds)
        {
            await publisher.PublishDelayResumeAsync(resumeMessage, delaySeconds, cancellationToken);
        }
        else
        {
            var token = await keeperAuth.GetServiceAccessTokenAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(token))
                return NodeExecutionResult.Fail("delay.wait: scheduler service account is not configured for long delays", retryable: false);

            var schedulerSettings = settings.Scheduler;
            var endpointUrl = ResolveHookUrl(schedulerSettings, schedulerSettings.DelayResumePath);
            var payload = JsonSerializer.Serialize(new WorkflowDelayResumeRequest
            {
                InstanceId = context.InstanceId,
                NodeId = node.Id,
                DomainName = context.DomainName,
                DomainId = context.DomainId,
                EdgeKey = "default"
            });

            var jobId = $"{schedulerSettings.JobIdPrefix}delay-{context.InstanceId}-{node.Id}";
            var job = new WorkflowSchedulerUserJobDto
            {
                JobId = jobId,
                JobType = 1,
                Name = $"Workflow delay {context.InstanceId}",
                Description = $"Resume workflow instance {context.InstanceId} node {node.Id}",
                CronExpression = WorkflowDelayCronHelper.ToOneShotCron(resumeAt),
                EndpointUrl = endpointUrl,
                HttpMethod = "POST",
                Payload = payload,
                IsActive = true,
                TimeoutSeconds = 120,
                MaxExecutionCount = 1
            };

            var existing = await scheduler.GetUserJobAsync(jobId, token, cancellationToken);
            if (existing == null)
                await scheduler.CreateUserJobAsync(job, token, cancellationToken);
            else
                await scheduler.UpdateUserJobAsync(job, token, cancellationToken);

            output["schedulerJobId"] = jobId;
        }

        return NodeExecutionResult.Wait(WorkflowWaitingTypes.Delay, output);
    }

    private static int ResolveDelaySeconds(WorkflowNodeDefinition node)
    {
        if (node.Config.TryGetValue("delaySeconds", out var raw) && raw != null)
        {
            if (raw is int i)
                return i;
            if (raw is long l)
                return (int)l;
            if (int.TryParse(raw.ToString(), out var parsed))
                return parsed;
        }

        return 0;
    }

    private static string ResolveHookUrl(SchedulerSettings scheduler, string path)
    {
        if (path.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return path;

        var baseUrl = scheduler.HookBaseUrl?.Trim().TrimEnd('/');
        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new InvalidOperationException("Scheduler HookBaseUrl is not configured.");

        return baseUrl + (path.StartsWith('/') ? path : "/" + path);
    }
}
