namespace MngDocument.Application.Contracts.CoverPages;

public sealed class CreateCoverPageRequest
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; } = true;
    public CoverPageDefinitionDto Definition { get; set; } = new();
    public CoverPageSettingsDto? Settings { get; set; }
}

public sealed class UpdateCoverPageRequest
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; } = true;
    public CoverPageDefinitionDto Definition { get; set; } = new();
    public CoverPageSettingsDto? Settings { get; set; }
}
