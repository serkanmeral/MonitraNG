using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MngScheduler.Application.Interfaces;
using MngScheduler.Domain.Entities;

namespace MngScheduler.Api.Controllers;

/// <summary>
/// User Job management controller (Domain-specific)
/// User jobs are stored in domain databases via MngDataGateway
/// </summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/user/jobs")]
[Authorize] // Require authentication
public class UserJobController : ControllerBase
{
    private readonly IUserJobService _jobService;
    private readonly ILogger<UserJobController> _logger;

    public UserJobController(
        IUserJobService jobService,
        ILogger<UserJobController> logger)
    {
        _jobService = jobService ?? throw new ArgumentNullException(nameof(jobService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Get current user's token from Authorization header
    /// </summary>
    private string? GetToken()
    {
        var authHeader = Request.Headers["Authorization"].FirstOrDefault();
        if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer "))
        {
            return authHeader.Substring("Bearer ".Length).Trim();
        }
        return null;
    }

    /// <summary>
    /// Get all user jobs for the current user's domain (active and inactive)
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<ScheduledJob>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetAllJobs()
    {
        try
        {
            var token = GetToken();
            var jobs = await _jobService.GetAllJobsAsync(token);
            return Ok(jobs);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access attempt to get user jobs");
            return Unauthorized(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all user jobs");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Get active user jobs for the current user's domain
    /// </summary>
    [HttpGet("active")]
    [ProducesResponseType(typeof(IEnumerable<ScheduledJob>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetActiveJobs()
    {
        try
        {
            var token = GetToken();
            var jobs = await _jobService.GetActiveJobsAsync(token);
            return Ok(jobs);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access attempt to get active user jobs");
            return Unauthorized(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting active user jobs");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Get user job by ID (from current user's domain)
    /// </summary>
    [HttpGet("{jobId}")]
    [ProducesResponseType(typeof(ScheduledJob), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetJobById(string jobId)
    {
        try
        {
            var token = GetToken();
            var job = await _jobService.GetJobByIdAsync(jobId, token);
            if (job == null)
            {
                return NotFound(new { error = $"Job with ID '{jobId}' not found" });
            }

            return Ok(job);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access attempt to get user job: {JobId}", jobId);
            return Unauthorized(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user job by ID: {JobId}", jobId);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Create a new user job in the current user's domain
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ScheduledJob), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CreateJob([FromBody] ScheduledJob job)
    {
        try
        {
            var token = GetToken();
            var createdJob = await _jobService.CreateJobAsync(job, token);
            return CreatedAtAction(nameof(GetJobById), new { jobId = createdJob.JobId }, createdJob);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access attempt to create user job");
            return Unauthorized(new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Validation error creating user job");
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating user job");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Update an existing user job in the current user's domain
    /// </summary>
    [HttpPut("{jobId}")]
    [ProducesResponseType(typeof(ScheduledJob), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UpdateJob(string jobId, [FromBody] ScheduledJob job)
    {
        try
        {
            // Ensure jobId matches
            if (job.JobId != jobId)
            {
                return BadRequest(new { error = "JobId in URL does not match JobId in body" });
            }

            var token = GetToken();
            var updatedJob = await _jobService.UpdateJobAsync(job, token);
            return Ok(updatedJob);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access attempt to update user job: {JobId}", jobId);
            return Unauthorized(new { error = ex.Message });
        }
        catch (Domain.Exceptions.JobNotFoundException)
        {
            return NotFound(new { error = $"Job with ID '{jobId}' not found" });
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Validation error updating user job: {JobId}", jobId);
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating user job: {JobId}", jobId);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Delete a user job from the current user's domain
    /// </summary>
    [HttpDelete("{jobId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> DeleteJob(string jobId)
    {
        try
        {
            var token = GetToken();
            var deleted = await _jobService.DeleteJobAsync(jobId, token);
            if (!deleted)
            {
                return NotFound(new { error = $"Job with ID '{jobId}' not found" });
            }

            return NoContent();
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access attempt to delete user job: {JobId}", jobId);
            return Unauthorized(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting user job: {JobId}", jobId);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Get execution history for a user job (from current user's domain)
    /// </summary>
    [HttpGet("{jobId}/executions")]
    [ProducesResponseType(typeof(IEnumerable<JobExecution>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetJobExecutions(string jobId, [FromQuery] int limit = 100)
    {
        try
        {
            var token = GetToken();
            
            // Verify job exists and belongs to user's domain
            var job = await _jobService.GetJobByIdAsync(jobId, token);
            if (job == null)
            {
                return NotFound(new { error = $"Job with ID '{jobId}' not found" });
            }

            var executions = await _jobService.GetJobExecutionsAsync(jobId, limit, token);
            return Ok(executions);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access attempt to get user job executions: {JobId}", jobId);
            return Unauthorized(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting execution history for user job: {JobId}", jobId);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }
}
