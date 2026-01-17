using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MngLLM.Domain.Interfaces;

namespace MngLLM.Api.Controllers;

/// <summary>
/// Documentation Controller - Dokümantasyon arama ve yönetim için
/// </summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/docs")]
[Produces("application/json")]
public class DocumentationController : ControllerBase
{
    private readonly IDocumentationProvider _documentationProvider;
    private readonly ILogger<DocumentationController> _logger;

    public DocumentationController(
        IDocumentationProvider documentationProvider,
        ILogger<DocumentationController> logger)
    {
        _documentationProvider = documentationProvider;
        _logger = logger;
    }


    /// <summary>
    /// Search documentation
    /// </summary>
    /// <param name="query">Arama sorgusu</param>
    /// <param name="limit">Maksimum sonuç sayısı (varsayılan: 5)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Documentation sonuçları</returns>
    [HttpGet("search")]
    [Authorize(Policy = "AllowAnonymousInDevelopment")]
    [ProducesResponseType(typeof(List<DocumentationResult>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SearchAsync(
        [FromQuery] string query,
        [FromQuery] int limit = 5,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return BadRequest(new { error = "Query parameter is required" });
        }

        try
        {
            var results = await _documentationProvider.SearchAsync(query, limit, cancellationToken);
            return Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching documentation: {Query}", query);
            return StatusCode(500, new { error = "An error occurred while searching documentation" });
        }
    }

    /// <summary>
    /// Get document content by ID
    /// </summary>
    /// <param name="documentId">Dokümantasyon ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Dokümantasyon içeriği</returns>
    [HttpGet("{documentId}")]
    [Authorize(Policy = "AllowAnonymousInDevelopment")]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetContentAsync(
        string documentId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var content = await _documentationProvider.GetContentAsync(documentId, cancellationToken);
            return Ok(new { content });
        }
        catch (FileNotFoundException)
        {
            return NotFound(new { error = $"Document not found: {documentId}" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting document content: {DocumentId}", documentId);
            return StatusCode(500, new { error = "An error occurred while getting document content" });
        }
    }

    /// <summary>
    /// Get all indexed documents
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Tüm indekslenmiş dokümantasyonlar</returns>
    [HttpGet]
    [Authorize(Policy = "AllowAnonymousInDevelopment")]
    [ProducesResponseType(typeof(List<DocumentationIndex>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllDocumentsAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var documents = await _documentationProvider.GetAllDocumentsAsync(cancellationToken);
            return Ok(documents);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all documents");
            return StatusCode(500, new { error = "An error occurred while getting documents" });
        }
    }

    /// <summary>
    /// Re-index all documentation
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Re-indexing sonucu</returns>
    [HttpPost("reindex")]
    [Authorize(Policy = "AllowAnonymousInDevelopment")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ReindexAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _documentationProvider.ReindexAsync(cancellationToken);
            return Ok(new { message = "Re-indexing completed successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error re-indexing documentation");
            return StatusCode(500, new { error = "An error occurred while re-indexing documentation" });
        }
    }
}
