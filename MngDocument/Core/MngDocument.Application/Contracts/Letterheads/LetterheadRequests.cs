using MngDocument.Application.Contracts.Templates;

namespace MngDocument.Application.Contracts.Letterheads;

public sealed class CreateLetterheadRequest
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; } = true;
    public TemplateLetterheadDto Letterhead { get; set; } = new() { Enabled = true };
    public LetterheadSettingsDto? Settings { get; set; }
}

public sealed class UpdateLetterheadRequest
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; } = true;
    public TemplateLetterheadDto Letterhead { get; set; } = new() { Enabled = true };
    public LetterheadSettingsDto? Settings { get; set; }
}
