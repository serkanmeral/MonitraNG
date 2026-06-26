namespace MngDocument.Application.Configuration;

/// <summary>Domain-level corporate footer snapshot (Odak CoC-style dual office block).</summary>
public class DomainFooterProfileSettings
{
    public string FormCode { get; set; } = "F86";
    public string FormRevision { get; set; } = "Rev04";
    public string FormRevisionDate { get; set; } = "30.11.2022";
    public List<DomainOfficeSettings> Offices { get; set; } = new();
}

public class DomainOfficeSettings
{
    public string Label { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Fax { get; set; } = string.Empty;
}
