using Microsoft.AspNetCore.Mvc;
using MngWorkflow.Application.Contracts;
using MngWorkflow.Application.Services;
using MngWorkflow.Domain.Constants;

namespace MngWorkflow.Api.Controllers;

[ApiController]
[Route("api/v1/dev/triggers")]
public sealed class DevTriggersController : ControllerBase
{
    private readonly IWorkflowEventTriggerProcessor _processor;
    private readonly IConfiguration _configuration;

    public DevTriggersController(IWorkflowEventTriggerProcessor processor, IConfiguration configuration)
    {
        _processor = processor;
        _configuration = configuration;
    }

    [HttpPost("simulate")]
    public async Task<IActionResult> Simulate([FromBody] SimulateEventTriggerRequest request, CancellationToken cancellationToken)
    {
        if (!IsDevEndpointEnabled())
            return NotFound();

        var domainName = string.IsNullOrWhiteSpace(request.DomainName) ? "odak" : request.DomainName.Trim();
        var domainId = string.IsNullOrWhiteSpace(request.DomainId) ? domainName : request.DomainId.Trim();
        var eventType = request.EventType?.Trim() ?? "oc.workitem.created";

        var payload = request.Payload ?? new Dictionary<string, object?>();
        payload["domainName"] = domainName;
        payload["domainId"] = domainId;
        if (!payload.ContainsKey("eventId"))
            payload["eventId"] = Guid.NewGuid().ToString("N");

        var routingKey = $"{domainId}.{eventType}";
        var body = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(payload);

        await _processor.ProcessAsync(WorkflowEventExchanges.OcEvents, routingKey, body, cancellationToken);
        return Accepted(new { routingKey, eventType, domainName, status = "processed" });
    }

    [HttpPost("schedule/simulate")]
    public async Task<IActionResult> SimulateSchedule(
        [FromBody] SimulateScheduleTriggerRequest request,
        [FromServices] IWorkflowHookService hooks,
        CancellationToken cancellationToken)
    {
        if (!IsDevEndpointEnabled())
            return NotFound();

        var domainName = string.IsNullOrWhiteSpace(request.DomainName) ? "odak" : request.DomainName.Trim();
        var domainId = string.IsNullOrWhiteSpace(request.DomainId) ? domainName : request.DomainId.Trim();

        if (string.IsNullOrWhiteSpace(request.WorkflowId))
            return BadRequest(new { error = "workflowId is required." });

        var result = await hooks.RunScheduleTriggerAsync(new WorkflowScheduleRunRequest
        {
            WorkflowId = request.WorkflowId.Trim(),
            WorkflowVersionId = request.WorkflowVersionId,
            DomainName = domainName,
            DomainId = domainId
        }, cancellationToken);

        return Accepted(new { request.WorkflowId, result.InstanceId, result.CorrelationId, status = "started" });
    }

    private bool IsDevEndpointEnabled() =>
        _configuration.GetValue<bool>("EnableDevWorkflowEndpoints")
        || string.Equals(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"), "Development", StringComparison.OrdinalIgnoreCase);
}

public sealed class SimulateEventTriggerRequest
{
    public string? DomainName { get; set; }
    public string? DomainId { get; set; }
    public string? EventType { get; set; }
    public Dictionary<string, object?>? Payload { get; set; }
}

public sealed class SimulateScheduleTriggerRequest
{
    public string WorkflowId { get; set; } = string.Empty;
    public string? WorkflowVersionId { get; set; }
    public string? DomainName { get; set; }
    public string? DomainId { get; set; }
}
