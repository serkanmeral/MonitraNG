using System.Text.Json;
using Microsoft.Extensions.Logging;
using MngWorkflow.Application.Execution;
using MngWorkflow.Application.Repositories;
using MngWorkflow.Application.Services;
using MngWorkflow.Domain.Enums;
using MngWorkflow.Infrastructure.Messaging;
using MngWorkflow.Infrastructure.Utilities;

namespace MngWorkflow.Infrastructure.Services;

public sealed class WorkflowEventTriggerProcessor : IWorkflowEventTriggerProcessor
{
    private readonly IWorkflowTriggerRepository _triggers;
    private readonly IWorkflowVersionRepository _versions;
    private readonly IWorkflowRunService _runs;
    private readonly IWorkflowExpressionEvaluator _expressions;
    private readonly ILogger<WorkflowEventTriggerProcessor> _logger;

    public WorkflowEventTriggerProcessor(
        IWorkflowTriggerRepository triggers,
        IWorkflowVersionRepository versions,
        IWorkflowRunService runs,
        IWorkflowExpressionEvaluator expressions,
        ILogger<WorkflowEventTriggerProcessor> logger)
    {
        _triggers = triggers;
        _versions = versions;
        _runs = runs;
        _expressions = expressions;
        _logger = logger;
    }

    public async Task ProcessAsync(
        string exchange,
        string routingKey,
        ReadOnlyMemory<byte> body,
        CancellationToken cancellationToken = default)
    {
        var parsed = WorkflowEventRouting.ParseTopicRoutingKey(routingKey);
        if (parsed == null)
        {
            _logger.LogDebug("Skip event with unparseable routing key {RoutingKey}", routingKey);
            return;
        }

        var (domainId, eventType) = parsed.Value;
        var payload = ParsePayload(body);
        var domainName = payload.TryGetValue("domainName", out var dn) ? dn?.ToString() : null;
        if (string.IsNullOrWhiteSpace(domainName))
            domainName = domainId;

        var bindings = await _triggers.FindByEventTypeAsync(domainName, eventType, cancellationToken);
        if (bindings.Count == 0)
            return;

        foreach (var binding in bindings)
        {
            if (!binding.Enabled)
                continue;

            if (!string.IsNullOrWhiteSpace(binding.FilterExpression) &&
                !EvaluateFilter(binding.FilterExpression, domainId, domainName, payload))
            {
                continue;
            }

            var version = await _versions.GetByIdAsync(domainName, binding.WorkflowVersionId, cancellationToken);
            if (version == null || version.Status != WorkflowVersionStatus.Published)
                continue;

            var correlationId = payload.TryGetValue("eventId", out var eid) ? eid?.ToString() : null;
            var result = await _runs.StartEventRunAsync(version, payload, correlationId, cancellationToken);

            _logger.LogInformation(
                "Event trigger started workflow={WorkflowId} instance={InstanceId} eventType={EventType} exchange={Exchange}",
                binding.WorkflowId,
                result.InstanceId,
                eventType,
                exchange);
        }
    }

    private bool EvaluateFilter(
        string filterExpression,
        string domainId,
        string domainName,
        Dictionary<string, object?> payload)
    {
        try
        {
            var context = new WorkflowExecutionContext
            {
                InstanceId = "event-trigger",
                WorkflowVersionId = "n/a",
                DomainId = domainId,
                DomainName = domainName,
                CorrelationId = payload.TryGetValue("eventId", out var eid) ? eid?.ToString() ?? "n/a" : "n/a",
                Event = payload,
                Variables = new Dictionary<string, object?>(),
                Outputs = new Dictionary<string, object?>()
            };

            return _expressions.EvaluateBoolean(filterExpression, context);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Filter expression failed: {Expression}", filterExpression);
            return false;
        }
    }

    private static Dictionary<string, object?> ParsePayload(ReadOnlyMemory<byte> body)
    {
        if (body.IsEmpty)
            return new Dictionary<string, object?>(StringComparer.Ordinal);

        var payload = JsonSerializer.Deserialize<Dictionary<string, object?>>(body.Span)
            ?? new Dictionary<string, object?>(StringComparer.Ordinal);

        return WorkflowJsonNormalizer.NormalizeDictionary(payload);
    }
}
