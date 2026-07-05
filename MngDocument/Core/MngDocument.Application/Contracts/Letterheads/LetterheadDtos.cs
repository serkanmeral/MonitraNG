using MngDocument.Application.Contracts.Templates;

namespace MngDocument.Application.Contracts.Letterheads;

public sealed class LetterheadListResult
{
    public IReadOnlyList<LetterheadDto> Items { get; init; } = Array.Empty<LetterheadDto>();
    public long Total { get; init; }
}

public sealed class LetterheadDto
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Code { get; init; } = string.Empty;
    public string? Description { get; init; }
    public bool IsDefault { get; init; }
    public bool IsActive { get; init; } = true;
    public TemplateLetterheadDto Letterhead { get; init; } = new() { Enabled = true };
    public LetterheadSettingsDto Settings { get; init; } = new();
    public string? DesignStoragePath { get; init; }
    public string? DesignFileName { get; init; }
    public bool HasDesign { get; init; }
    public string? CreatedBy { get; init; }
    public DateTime? CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

public sealed class LetterheadResolveResult
{
    public TemplateLetterheadDto? Letterhead { get; init; }
    public LetterheadSettingsDto Settings { get; init; } = new();
    public LetterheadFooterSettingsDto Footer { get; init; } = LetterheadBrandingDefaults.DefaultFooterSettings();
    public TemplatePageLayoutDto PageLayout { get; init; } = LetterheadBrandingDefaults.DefaultPageLayoutDto();
    public string? LetterheadId { get; init; }
    public string? LetterheadCode { get; init; }
    public string? LetterheadName { get; init; }
}
