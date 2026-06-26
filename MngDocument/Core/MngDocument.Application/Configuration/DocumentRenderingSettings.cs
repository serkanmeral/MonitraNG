namespace MngDocument.Application.Configuration;

/// <summary>
/// On-prem DOCX→PDF dönüşümü (Gotenberg = headless LibreOffice motoru).
/// </summary>
public class DocumentRenderingSettings
{
    public bool Enabled { get; set; } = true;

    /// <summary>Örn. http://gotenberg:3000 — yalnızca iç ağ.</summary>
    public string GotenbergBaseUrl { get; set; } = "http://gotenberg:3000";

    public int TimeoutSeconds { get; set; } = 120;
}
