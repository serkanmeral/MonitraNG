using Microsoft.AspNetCore.Mvc;
using MngKeeper.Application.DTOs.Template;
using MngKeeper.Application.Interfaces;
using MngKeeper.Domain.Entities;
using MngKeeper.Application.DTOs;

namespace MngKeeper.Api.Controllers;

/// <summary>
/// Template management controller
/// </summary>
[ApiController]
[Route("api/templates")]
[Produces("application/json")]
[ApiExplorerSettings(GroupName = "Template Management")]
public class TemplatesController : ControllerBase
{
    private readonly ITemplateService _templateService;
    private readonly IDomainRepository _domainRepository;
    private readonly ILogger<TemplatesController> _logger;

    public TemplatesController(
        ITemplateService templateService,
        IDomainRepository domainRepository,
        ILogger<TemplatesController> logger)
    {
        _templateService = templateService;
        _domainRepository = domainRepository;
        _logger = logger;
    }

    /// <summary>
    /// Get all templates
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<TemplateResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllTemplates(CancellationToken cancellationToken)
    {
        try
        {
            var templates = await _templateService.GetAllTemplatesAsync(cancellationToken);
            var response = templates.Select(MapToResponseDto).ToList();
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all templates");
            return StatusCode(500, new { error = "Internal server error", message = ex.Message });
        }
    }

    /// <summary>
    /// Get template by name
    /// </summary>
    [HttpGet("{name}")]
    [ProducesResponseType(typeof(TemplateResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTemplate(string name, CancellationToken cancellationToken)
    {
        try
        {
            var template = await _templateService.GetTemplateAsync(name, cancellationToken);
            if (template == null)
            {
                return NotFound(new { error = $"Template '{name}' not found" });
            }

            return Ok(MapToResponseDto(template));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting template: {TemplateName}", name);
            return StatusCode(500, new { error = "Internal server error", message = ex.Message });
        }
    }

    /// <summary>
    /// Get templates by source domain ID
    /// </summary>
    [HttpGet("domain/{domainId}")]
    [ProducesResponseType(typeof(List<TemplateResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTemplatesByDomain(string domainId, CancellationToken cancellationToken)
    {
        try
        {
            var templates = await _templateService.GetTemplatesBySourceDomainAsync(domainId, cancellationToken);
            var response = templates.Select(MapToResponseDto).ToList();
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting templates for domain: {DomainId}", domainId);
            return StatusCode(500, new { error = "Internal server error", message = ex.Message });
        }
    }

    /// <summary>
    /// Create a new template
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(TemplateResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateTemplate([FromBody] CreateTemplateDto dto, CancellationToken cancellationToken)
    {
        try
        {
            // Validate source domain
            var sourceDomain = await _domainRepository.GetByIdAsync(dto.SourceDomainId);
            if (sourceDomain == null)
            {
                return BadRequest(new { error = $"Source domain '{dto.SourceDomainId}' not found" });
            }

            // Convert DTO to domain entities
            var collections = dto.Collections.Select(c => new SelectedCollection
            {
                CollectionName = c.CollectionName,
                IncludeIndexes = c.IncludeIndexes
            }).ToList();

            // Get current user (TODO: Get from JWT context)
            var createdBy = "system"; // TODO: Get from JWT context

            // Create template
            var template = await _templateService.CreateTemplateAsync(
                dto.Name,
                dto.Description ?? string.Empty,
                dto.SourceDomainId,
                sourceDomain.DatabaseName,
                collections,
                createdBy,
                cancellationToken);

            return CreatedAtAction(nameof(GetTemplate), new { name = template.Name }, MapToResponseDto(template));
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid operation while creating template");
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating template");
            return StatusCode(500, new { error = "Internal server error", message = ex.Message });
        }
    }

    /// <summary>
    /// Update template
    /// </summary>
    [HttpPut("{name}")]
    [ProducesResponseType(typeof(TemplateResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateTemplate(string name, [FromBody] UpdateTemplateDto dto, CancellationToken cancellationToken)
    {
        try
        {
            // Convert DTO to domain entities
            var collections = dto.Collections.Select(c => new SelectedCollection
            {
                CollectionName = c.CollectionName,
                IncludeIndexes = c.IncludeIndexes
            }).ToList();

            // Get current user (TODO: Get from JWT context)
            var updatedBy = "system"; // TODO: Get from JWT context

            // Update template
            var template = await _templateService.UpdateTemplateAsync(
                name,
                dto.Description,
                collections,
                updatedBy,
                cancellationToken);

            return Ok(MapToResponseDto(template));
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid operation while updating template: {TemplateName}", name);
            if (ex.Message.Contains("not found"))
            {
                return NotFound(new { error = ex.Message });
            }
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating template: {TemplateName}", name);
            return StatusCode(500, new { error = "Internal server error", message = ex.Message });
        }
    }

    /// <summary>
    /// Delete template
    /// </summary>
    [HttpDelete("{name}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteTemplate(string name, CancellationToken cancellationToken)
    {
        try
        {
            var deleted = await _templateService.DeleteTemplateAsync(name, cancellationToken);
            if (!deleted)
            {
                return NotFound(new { error = $"Template '{name}' not found" });
            }

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting template: {TemplateName}", name);
            return StatusCode(500, new { error = "Internal server error", message = ex.Message });
        }
    }

    /// <summary>
    /// Get template content (from MinIO)
    /// </summary>
    [HttpGet("{name}/content")]
    [ProducesResponseType(typeof(TemplateContent), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTemplateContent(string name, CancellationToken cancellationToken)
    {
        try
        {
            var content = await _templateService.GetTemplateContentAsync(name, cancellationToken);
            if (content == null)
            {
                return NotFound(new { error = $"Template content for '{name}' not found" });
            }

            return Ok(content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting template content: {TemplateName}", name);
            return StatusCode(500, new { error = "Internal server error", message = ex.Message });
        }
    }

    /// <summary>
    /// Map Template entity to response DTO
    /// </summary>
    private static TemplateResponseDto MapToResponseDto(Template template)
    {
        return new TemplateResponseDto
        {
            Id = template.Id,
            Name = template.Name,
            Description = template.Description,
            SourceDomainId = template.SourceDomainId,
            SourceDatabaseName = template.SourceDatabaseName,
            Collections = template.Collections.Select(c => new SelectedCollectionResponseDto
            {
                CollectionName = c.CollectionName,
                IncludeIndexes = c.IncludeIndexes,
                DocumentCount = c.DocumentCount
            }).ToList(),
            TotalDocumentCount = template.TotalDocumentCount,
            CreatedAt = template.CreatedAt,
            CreatedBy = template.CreatedBy,
            UpdatedAt = template.UpdatedAt,
            UpdatedBy = template.UpdatedBy
        };
    }
}
