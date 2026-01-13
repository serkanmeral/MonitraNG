using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using MngAdmin.Application.Backup.DTOs;
using MngAdmin.Application.Backup.Services;

namespace MngAdmin.Api.Controllers;

/// <summary>
/// Backup management controller
/// </summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/backup")]
public class BackupController : ControllerBase
{
    private readonly ILogger<BackupController> _logger;
    private readonly IBackupService _backupService;

    public BackupController(
        ILogger<BackupController> logger,
        IBackupService backupService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _backupService = backupService ?? throw new ArgumentNullException(nameof(backupService));
    }

    private string GetCurrentUserId()
    {
        // Authentication disabled - return system user
        return "system";
    }

    /// <summary>
    /// Create system backup (MongoDB or PostgreSQL)
    /// </summary>
    [HttpPost("system")]
    [ProducesResponseType(typeof(BackupResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateSystemBackup([FromBody] BackupRequestDto request, CancellationToken cancellationToken)
    {
        try
        {
            var userId = GetCurrentUserId();
            var result = await _backupService.CreateSystemBackupAsync(request, userId, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create system backup");
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Create system MongoDB backup
    /// </summary>
    [HttpPost("system/mongodb")]
    [ProducesResponseType(typeof(BackupResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateSystemMongoBackup([FromBody] BackupRequestDto? request, CancellationToken cancellationToken)
    {
        try
        {
            var userId = GetCurrentUserId();
            var backupRequest = request ?? new BackupRequestDto { DatabaseType = "mongodb" };
            backupRequest.DatabaseType = "mongodb";
            var result = await _backupService.CreateSystemBackupAsync(backupRequest, userId, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create system MongoDB backup");
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Create system PostgreSQL backup
    /// </summary>
    [HttpPost("system/postgresql")]
    [ProducesResponseType(typeof(BackupResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateSystemPostgresBackup([FromBody] BackupRequestDto? request, CancellationToken cancellationToken)
    {
        try
        {
            var userId = GetCurrentUserId();
            var backupRequest = request ?? new BackupRequestDto { DatabaseType = "postgresql" };
            backupRequest.DatabaseType = "postgresql";
            var result = await _backupService.CreateSystemBackupAsync(backupRequest, userId, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create system PostgreSQL backup");
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Create domain backup (MongoDB only)
    /// </summary>
    [HttpPost("domain/{domainName}")]
    [ProducesResponseType(typeof(BackupResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateDomainBackup(
        [FromRoute] string domainName,
        [FromBody] BackupRequestDto? request,
        CancellationToken cancellationToken)
    {
        try
        {
            var userId = GetCurrentUserId();
            var backupRequest = request ?? new BackupRequestDto { DatabaseType = "mongodb" };
            var result = await _backupService.CreateDomainBackupAsync(domainName, backupRequest, userId, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Domain not found: {DomainName}", domainName);
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create domain backup: {DomainName}", domainName);
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get backup status by ID
    /// </summary>
    [HttpGet("{backupId}")]
    [ProducesResponseType(typeof(BackupResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBackupStatus([FromRoute] string backupId, CancellationToken cancellationToken)
    {
        var result = await _backupService.GetBackupStatusAsync(backupId, cancellationToken);
        if (result == null)
        {
            return NotFound(new { error = "Backup not found" });
        }
        return Ok(result);
    }

    /// <summary>
    /// Get system backup list
    /// </summary>
    [HttpGet("system")]
    [ProducesResponseType(typeof(BackupListResponseDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSystemBackups([FromQuery] string? databaseName, CancellationToken cancellationToken)
    {
        var result = await _backupService.GetSystemBackupsAsync(databaseName, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Get domain backup list
    /// </summary>
    [HttpGet("domain/{domainName}")]
    [ProducesResponseType(typeof(BackupListResponseDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDomainBackups(
        [FromRoute] string domainName,
        [FromQuery] string? databaseName,
        CancellationToken cancellationToken)
    {
        var result = await _backupService.GetDomainBackupsAsync(domainName, databaseName, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Create full backup (system backups + all domain backups)
    /// </summary>
    /// <remarks>
    /// This endpoint performs a complete backup of the system:
    /// 1. Creates backups for all system MongoDB databases (mngkeeper, mngtemplates)
    /// 2. Creates backups for all system PostgreSQL databases (keycloak, etc.)
    /// 3. Discovers all domain databases (mng_* pattern) from MongoDB
    /// 4. Creates backups for each discovered domain
    /// </remarks>
    [HttpPost("full")]
    [ProducesResponseType(typeof(FullBackupResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateFullBackup(CancellationToken cancellationToken)
    {
        try
        {
            var userId = GetCurrentUserId();
            _logger.LogInformation("Full backup requested by user: {UserId}", userId);
            var result = await _backupService.CreateFullBackupAsync(userId, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create full backup");
            return BadRequest(new { error = ex.Message });
        }
    }
}
