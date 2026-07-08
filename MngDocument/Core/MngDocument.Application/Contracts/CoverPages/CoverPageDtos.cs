using MngDocument.Application.Contracts.Letterheads;
using MngDocument.Application.Contracts.Templates;

namespace MngDocument.Application.Contracts.CoverPages;

public sealed class CoverPageListResult
{
    public IReadOnlyList<CoverPageDto> Items { get; init; } = Array.Empty<CoverPageDto>();
    public long Total { get; init; }
}

public sealed class CoverPageDefinitionDto
{
    public bool ShowLogo { get; set; } = true;
    public bool ShowDocumentName { get; set; } = true;
    public bool ShowDocNo { get; set; } = true;
    public bool ShowGeneratedAt { get; set; } = true;
    public bool ShowCustomerName { get; set; } = true;
}

public sealed class CoverPageSettingsDto
{
    public TemplatePageLayoutDto PageLayout { get; set; } = LetterheadBrandingDefaults.DefaultPageLayoutDto();
}

public sealed class CoverPageDto
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Code { get; init; } = string.Empty;
    public string? Description { get; init; }
    public bool IsDefault { get; init; }
    public bool IsActive { get; init; } = true;
    public CoverPageDefinitionDto Definition { get; init; } = new();
    public CoverPageSettingsDto Settings { get; init; } = new();
    public string? DesignStoragePath { get; init; }
    public string? DesignFileName { get; init; }
    public bool HasDesign { get; init; }
    public string? CreatedBy { get; init; }
    public DateTime? CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

public sealed class CoverPageResolveResult
{
    public CoverPageDto? CoverPage { get; init; }
    public string? CoverPageId { get; init; }
    public string? CoverPageCode { get; init; }
    public string? CoverPageName { get; init; }
}
