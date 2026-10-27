namespace MngKeeper.Api.Contracts;

/// <summary>
/// Public login-page branding. Must not include mail, directory, license, or admin fields.
/// </summary>
public sealed class DomainBrandingResponse
{
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Logo { get; set; }
    public string? LogoDark { get; set; }
    public string? LogoLight { get; set; }
    public string? LogoUrl { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
