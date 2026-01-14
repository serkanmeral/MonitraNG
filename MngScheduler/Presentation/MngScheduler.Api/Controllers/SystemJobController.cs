using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MngScheduler.Application.Interfaces;
using MngScheduler.Domain.Entities;

namespace MngScheduler.Api.Controllers;

/// <summary>
/// System Job management controller (Admin only)
/// System jobs are stored in mng_keeper database
/// </summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/system/jobs")]
// [Authorize] // Require authentication - TEMPORARILY DISABLED FOR TESTING
public class SystemJobController : ControllerBase
{
    private readonly ISystemJobService _jobService;
    private readonly ILogger<SystemJobController> _logger;

    public SystemJobController(
        ISystemJobService jobService,
        ILogger<SystemJobController> logger)
    {
        _jobService = jobService ?? throw new ArgumentNullException(nameof(jobService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Check if current user is admin
    /// </summary>
    private bool IsAdmin()
    {
        // TEMPORARILY DISABLED FOR TESTING - Always return true
        return true;
        
        // var user = HttpContext.User;
        // var isAdminClaim = user.FindFirst("isAdmin")?.Value;
        // return isAdminClaim?.ToLowerInvariant() == "true";
    }

    /// <summary>
    /// Get all system jobs (active and inactive)
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<ScheduledJob>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAllJobs()
    {
        if (!IsAdmin())
        {
            return Forbid("Only administrators can access system jobs");
        }

        try
        {
            var jobs = await _jobService.GetAllJobsAsync();
            return Ok(jobs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all system jobs");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Get active system jobs only
    /// </summary>
    [HttpGet("active")]
    [ProducesResponseType(typeof(IEnumerable<ScheduledJob>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetActiveJobs()
    {
        if (!IsAdmin())
        {
            return Forbid("Only administrators can access system jobs");
        }

        try
        {
            var jobs = await _jobService.GetActiveJobsAsync();
            return Ok(jobs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting active system jobs");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Get system job by ID
    /// </summary>
    [HttpGet("{jobId}")]
    [ProducesResponseType(typeof(ScheduledJob), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetJobById(string jobId)
    {
        if (!IsAdmin())
        {
            return Forbid("Only administrators can access system jobs");
        }

        try
        {
            var job = await _jobService.GetJobByIdAsync(jobId);
            if (job == null)
            {
                return NotFound(new { error = $"Job with ID '{jobId}' not found" });
            }

            return Ok(job);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting system job by ID: {JobId}", jobId);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Create a new system job
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ScheduledJob), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateJob([FromBody] ScheduledJob job)
    {
        if (!IsAdmin())
        {
            return Forbid("Only administrators can create system jobs");
        }

        try
        {
            var createdJob = await _jobService.CreateJobAsync(job);
            return CreatedAtAction(nameof(GetJobById), new { jobId = createdJob.JobId }, createdJob);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Validation error creating system job");
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating system job");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Update an existing system job
    /// </summary>
    [HttpPut("{jobId}")]
    [ProducesResponseType(typeof(ScheduledJob), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdateJob(string jobId, [FromBody] ScheduledJob job)
    {
        if (!IsAdmin())
        {
            return Forbid("Only administrators can update system jobs");
        }

        try
        {
            // Ensure jobId matches
            if (job.JobId != jobId)
            {
                return BadRequest(new { error = "JobId in URL does not match JobId in body" });
            }

            var updatedJob = await _jobService.UpdateJobAsync(job);
            return Ok(updatedJob);
        }
        catch (Domain.Exceptions.JobNotFoundException)
        {
            return NotFound(new { error = $"Job with ID '{jobId}' not found" });
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Validation error updating system job: {JobId}", jobId);
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating system job: {JobId}", jobId);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Delete a system job
    /// </summary>
    [HttpDelete("{jobId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DeleteJob(string jobId)
    {
        if (!IsAdmin())
        {
            return Forbid("Only administrators can delete system jobs");
        }

        try
        {
            var deleted = await _jobService.DeleteJobAsync(jobId);
            if (!deleted)
            {
                return NotFound(new { error = $"Job with ID '{jobId}' not found" });
            }

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting system job: {JobId}", jobId);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Get execution history for a system job
    /// </summary>
    [HttpGet("{jobId}/executions")]
    [ProducesResponseType(typeof(IEnumerable<JobExecution>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetJobExecutions(string jobId, [FromQuery] int limit = 100)
    {
        if (!IsAdmin())
        {
            return Forbid("Only administrators can access system job executions");
        }

        try
        {
            // Verify job exists
            var job = await _jobService.GetJobByIdAsync(jobId);
            if (job == null)
            {
                return NotFound(new { error = $"Job with ID '{jobId}' not found" });
            }

            var executions = await _jobService.GetJobExecutionsAsync(jobId, limit);
            return Ok(executions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting execution history for system job: {JobId}", jobId);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }
}
