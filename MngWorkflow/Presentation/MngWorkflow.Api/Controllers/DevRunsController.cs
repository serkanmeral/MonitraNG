using Microsoft.AspNetCore.Mvc;
using MngWorkflow.Application.Services;
using MngWorkflow.Infrastructure.Http;

namespace MngWorkflow.Api.Controllers;

[ApiController]
[Route("api/v1/dev/runs")]
public sealed class DevRunsController : ControllerBase
{
    private readonly IWorkflowRunService _runService;
    private readonly IConfiguration _configuration;

    public DevRunsController(IWorkflowRunService runService, IConfiguration configuration)
    {
        _runService = runService;
        _configuration = configuration;
    }

    [HttpPost("smoke")]
    public async Task<IActionResult> StartSmoke([FromBody] SmokeRunRequest request, CancellationToken cancellationToken)
    {
        if (!IsDevEndpointEnabled())
            return NotFound();

        var domainName = string.IsNullOrWhiteSpace(request.DomainName) ? "odak" : request.DomainName.Trim();
        var domainId = string.IsNullOrWhiteSpace(request.DomainId) ? "odak-dev" : request.DomainId.Trim();
        var value = request.EventValue ?? 10;

        var result = await _runService.StartSmokeRunAsync(domainName, domainId, value, cancellationToken);
        return Accepted(new
        {
            result.InstanceId,
            result.CorrelationId,
            result.WorkflowVersionId,
            result.EntryNodeId,
            status = "queued"
        });
    }

    [HttpGet("{instanceId}/executions")]
    public async Task<IActionResult> GetExecutions(string instanceId, CancellationToken cancellationToken = default)
    {
        if (!IsDevEndpointEnabled())
            return NotFound();

        var detail = await _runService.GetRunDetailAsync(instanceId, cancellationToken);
        if (detail == null)
            return NotFound();

        return Ok(detail.Executions);
    }

    private bool IsDevEndpointEnabled() =>
        _configuration.GetValue<bool>("EnableDevWorkflowEndpoints")
        || string.Equals(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"), "Development", StringComparison.OrdinalIgnoreCase);
}

public sealed class SmokeRunRequest
{
    public string? DomainName { get; set; }
    public string? DomainId { get; set; }
    public int? EventValue { get; set; }
}
