using Microsoft.AspNetCore.Mvc;
using MngWorkflow.Application.Contracts;
using MngWorkflow.Application.Services;

namespace MngWorkflow.Api.Controllers;

[ApiController]
[Route("api/v1/definitions")]
public sealed class WorkflowDefinitionsController : ControllerBase
{
    private readonly IWorkflowDefinitionService _definitions;
    private readonly IWorkflowVersionService _versions;

    public WorkflowDefinitionsController(IWorkflowDefinitionService definitions, IWorkflowVersionService versions)
    {
        _definitions = definitions;
        _versions = versions;
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken) =>
        Ok(await _definitions.ListAsync(cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateWorkflowDefinitionRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var doc = await _definitions.CreateAsync(request, cancellationToken);
            return CreatedAtAction(nameof(Get), new { workflowId = doc.Id }, doc);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    [HttpGet("{workflowId}")]
    public async Task<IActionResult> Get(string workflowId, CancellationToken cancellationToken)
    {
        var doc = await _definitions.GetAsync(workflowId, cancellationToken);
        return doc == null ? NotFound() : Ok(doc);
    }

    [HttpPut("{workflowId}")]
    public async Task<IActionResult> Update(string workflowId, [FromBody] UpdateWorkflowDefinitionRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var doc = await _definitions.UpdateAsync(workflowId, request, cancellationToken);
            return doc == null ? NotFound() : Ok(doc);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("{workflowId}/versions")]
    public async Task<IActionResult> ListVersions(string workflowId, CancellationToken cancellationToken) =>
        Ok(await _versions.ListByWorkflowAsync(workflowId, cancellationToken));

    [HttpPost("{workflowId}/versions")]
    public async Task<IActionResult> CreateVersion(string workflowId, [FromBody] CreateWorkflowVersionRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var version = await _versions.CreateDraftAsync(workflowId, request, cancellationToken);
            return Created($"/api/v1/versions/{version.Id}", version);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
