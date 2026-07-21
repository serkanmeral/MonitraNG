namespace MngLLM.Application.DTOs.Di;

/// <summary>MVP extract result for Turkish e-Archive / UBL-TR invoices.</summary>
public sealed class EarsivFaturaExtractDto
{
    public string SchemaId { get; set; } = "earsiv_fatura";
    public int SchemaVersion { get; set; } = 1;
    public string ProfileId { get; set; } = string.Empty;
    public string InvoiceType { get; set; } = string.Empty;
    public string InvoiceId { get; set; } = string.Empty;
    public string Uuid { get; set; } = string.Empty;
    public string IssueDate { get; set; } = string.Empty;
    public string Currency { get; set; } = string.Empty;
    public decimal PayableAmount { get; set; }
    public decimal? TaxExclusiveAmount { get; set; }
    public string? SupplierName { get; set; }
    public string? SupplierVkn { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerVkn { get; set; }
    public List<EarsivFaturaLineDto>? Lines { get; set; }
    public string Source { get; set; } = "ubl_xml";
    public double Confidence { get; set; } = 1.0;
    public string? ResourceId { get; set; }
}

public sealed class EarsivFaturaLineDto
{
    public string? LineId { get; set; }
    public string? Name { get; set; }
    public decimal? Quantity { get; set; }
    public decimal? LineExtensionAmount { get; set; }
}
