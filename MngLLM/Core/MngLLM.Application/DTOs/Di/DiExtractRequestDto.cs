namespace MngLLM.Application.DTOs.Di;

public sealed class DiExtractRequestDto
{
    /// <summary>Document Intelligence resource id (dm_resources).</summary>
    public string ResourceId { get; set; } = string.Empty;

    /// <summary>Extract schema id. Default: earsiv_fatura.</summary>
    public string Schema { get; set; } = "earsiv_fatura";
}
