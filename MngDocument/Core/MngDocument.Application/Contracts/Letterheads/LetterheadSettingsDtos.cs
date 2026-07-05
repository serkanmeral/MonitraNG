using MngDocument.Application.Contracts.Templates;

namespace MngDocument.Application.Contracts.Letterheads;

public sealed class LetterheadHeaderFieldsDto
{
    public bool DocumentName { get; init; } = true;
    public bool DocNo { get; init; } = true;
    public bool GeneratedAt { get; init; } = true;
    public bool CreatePerson { get; init; }
}

public sealed class LetterheadGeneralDocNoDto
{
    public bool Enabled { get; init; } = true;
    public string Format { get; init; } = LetterheadBrandingDefaults.DefaultGeneralDocNoFormat;
    /// <summary>letterhead | global | custom</summary>
    public string ScopeMode { get; init; } = "letterhead";
    public string? ScopeKey { get; init; }
    public string ResetPolicy { get; init; } = "yearly";
    public int StartValue { get; init; } = 1;
    public int IncrementStep { get; init; } = 1;
}

public sealed class LetterheadSettingsDto
{
    public LetterheadHeaderFieldsDto HeaderFields { get; init; } = new();
    public LetterheadGeneralDocNoDto GeneralDocNo { get; init; } = new();
    public LetterheadFooterSettingsDto Footer { get; init; } = LetterheadBrandingDefaults.DefaultFooterSettings();
    /// <summary>Odak legacy boolean footer (generation fallback when LegacyOdakFooterEnabled).</summary>
    public TemplateFooterDto? LegacyOdakFooter { get; init; }
    public IReadOnlyList<FooterBlockDto> FooterBlocks { get; init; } = Array.Empty<FooterBlockDto>();
    public TemplatePageLayoutDto PageLayout { get; init; } = new();
}
