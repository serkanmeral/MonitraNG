using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MngDocument.Application.Contracts.Generation;
using MngDocument.Application.Contracts.Rendering;
using MngDocument.Application.Contracts.Templates;
using MngDocument.Application.Interfaces;

namespace MngDocument.Api.Controllers;

/// <summary>Document Designer — parametreli şablon tanımları (D1-alpha / D1-beta).</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/templates")]
[Authorize]
public sealed class DocumentTemplatesController : ControllerBase
{
    private readonly IDocumentTemplateService _templates;
    private readonly ITemplateEditorService _editor;
    private readonly IDocumentGenerationService _generation;

    public DocumentTemplatesController(
        IDocumentTemplateService templates,
        ITemplateEditorService editor,
        IDocumentGenerationService generation)
    {
        _templates = templates;
        _editor = editor;
        _generation = generation;
    }

    [HttpGet]
    [ProducesResponseType(typeof(TemplateListResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] string? categoryId, CancellationToken ct) =>
        Ok(await _templates.ListAsync(categoryId, ct));

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(TemplateDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(string id, CancellationToken ct) =>
        Ok(await _templates.GetByIdAsync(id, ct));

    [HttpPost("from-source")]
    [ProducesResponseType(typeof(TemplateDetailDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateFromSource(
        [FromBody] CreateTemplateFromSourceRequest request,
        CancellationToken ct)
    {
        var result = await _templates.CreateFromSourceAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id, version = "1.0" }, result);
    }

    [HttpPost("from-reference")]
    [ProducesResponseType(typeof(TemplateDetailDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateFromReference(
        [FromBody] CreateTemplateFromReferenceRequest request,
        CancellationToken ct)
    {
        var result = await _templates.CreateFromReferenceAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id, version = "1.0" }, result);
    }

    /// <summary>Mevcut şablonun DOCX + modelJson kopyasını hedef kategoride taslak olarak oluşturur.</summary>
    [HttpPost("{id}/duplicate")]
    [ProducesResponseType(typeof(TemplateDetailDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Duplicate(
        string id,
        [FromBody] DuplicateTemplateRequest request,
        CancellationToken ct)
    {
        var result = await _templates.DuplicateAsync(id, request, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id, version = "1.0" }, result);
    }

    /// <summary>Boş DOCX şablonu oluşturur (Belge Tasarımcısı E1).</summary>
    [HttpPost("blank")]
    [ProducesResponseType(typeof(TemplateDetailDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateBlank(
        [FromBody] CreateBlankTemplateRequest request,
        CancellationToken ct)
    {
        var result = await _editor.CreateBlankAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id, version = "1.0" }, result);
    }

    /// <summary>Collabora editör oturumu (iframe URL + WOPI token).</summary>
    [HttpGet("{id}/editor-session")]
    [ProducesResponseType(typeof(TemplateEditorSessionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEditorSession(string id, CancellationToken ct) =>
        Ok(await _editor.CreateEditorSessionAsync(id, ct));

    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(string id, CancellationToken ct)
    {
        await _templates.DeleteAsync(id, ct);
        return NoContent();
    }

    [HttpGet("source/{resourceId}/structure")]
    [ProducesResponseType(typeof(DocxStructureDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSourceStructure(string resourceId, CancellationToken ct) =>
        Ok(await _templates.GetSourceStructureAsync(resourceId, ct));

    [HttpGet("{id}/source/structure")]
    [ProducesResponseType(typeof(DocxStructureDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTemplateSourceStructure(string id, CancellationToken ct) =>
        Ok(await _templates.GetTemplateSourceStructureAsync(id, ct));

    [HttpPut("{id}/metadata")]
    [ProducesResponseType(typeof(TemplateDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateMetadata(
        string id,
        [FromBody] UpdateTemplateMetadataRequest request,
        CancellationToken ct) =>
        Ok(await _templates.UpdateMetadataAsync(id, request, ct));

    [HttpPut("{id}/letterhead")]
    [ProducesResponseType(typeof(TemplateDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateLetterhead(
        string id,
        [FromBody] UpdateTemplateLetterheadRequest request,
        CancellationToken ct) =>
        Ok(await _templates.UpdateLetterheadAsync(id, request, ct));

    [HttpPut("{id}/footer")]
    [ProducesResponseType(typeof(TemplateDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateFooter(
        string id,
        [FromBody] UpdateTemplateFooterRequest request,
        CancellationToken ct) =>
        Ok(await _templates.UpdateFooterAsync(id, request, ct));

    [HttpPut("{id}/page-structure")]
    [ProducesResponseType(typeof(TemplateDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdatePageStructure(
        string id,
        [FromBody] UpdateTemplatePageStructureRequest request,
        CancellationToken ct) =>
        Ok(await _templates.UpdatePageStructureAsync(id, request, ct));

    [HttpPost("{id}/publish")]
    [ProducesResponseType(typeof(TemplateDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Publish(string id, CancellationToken ct) =>
        Ok(await _templates.PublishAsync(id, ct));

    /// <summary>Yayınlanmış şablonu taslağa alır; belge üretimi yeniden yayınlanana kadar durur.</summary>
    [HttpPost("{id}/unpublish")]
    [ProducesResponseType(typeof(TemplateDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Unpublish(string id, CancellationToken ct) =>
        Ok(await _templates.UnpublishAsync(id, ct));

    [HttpPut("{id}/parameters")]
    [ProducesResponseType(typeof(TemplateDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateParameters(
        string id,
        [FromBody] UpdateTemplateParametersRequest request,
        CancellationToken ct) =>
        Ok(await _templates.UpdateParametersAsync(id, request, ct));

    /// <summary>Şablondan manuel döküman üretimi — merge + antet + kaynak ağacına kayıt (D4).</summary>
    [HttpPost("{id}/generate")]
    [ProducesResponseType(typeof(GenerateDocumentResultDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GenerateFromTemplate(
        string id,
        [FromBody] GenerateFromTemplateRequest request,
        CancellationToken ct)
    {
        var result = await _generation.GenerateFromTemplateAsync(id, request, ct);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>Şablondan üretim önizlemesi — parametre çözümlemesi (D4).</summary>
    [HttpPost("{id}/preview-generation")]
    [ProducesResponseType(typeof(DocumentGenerationPreviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PreviewGenerationFromTemplate(
        string id,
        [FromBody] PreviewFromTemplateRequest? request,
        CancellationToken ct) =>
        Ok(await _generation.PreviewFromTemplateAsync(id, request, ct));

    /// <summary>Şablondan üretim Collabora önizlemesi — merge + antet + salt okunur WOPI oturumu (D4).</summary>
    [HttpPost("{id}/preview-session")]
    [ProducesResponseType(typeof(TemplateGenerationPreviewSessionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> PreviewSessionFromTemplate(
        string id,
        [FromBody] PreviewFromTemplateRequest? request,
        CancellationToken ct) =>
        Ok(await _generation.CreatePreviewSessionFromTemplateAsync(id, request, ct));

    /// <summary>Şablon DOCX → merge → PDF (LibreOffice/Gotenberg). Altyapı smoke / önizleme.</summary>
    [HttpPost("{id}/render/pdf")]
    [Produces("application/pdf")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> RenderPdf(
        string id,
        [FromBody] RenderTemplatePdfRequest? request,
        CancellationToken ct)
    {
        try
        {
            var pdf = await _templates.RenderTemplatePdfAsync(id, request, ct);
            return File(pdf, "application/pdf", "template-preview.pdf");
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("rendering", StringComparison.OrdinalIgnoreCase)
                                                  || ex.Message.Contains("Gotenberg", StringComparison.OrdinalIgnoreCase))
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = ex.Message });
        }
    }
}
