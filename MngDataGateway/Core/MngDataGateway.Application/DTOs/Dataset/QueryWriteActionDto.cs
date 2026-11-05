namespace MngDataGateway.Application.DTOs.Dataset;

/// <summary>
/// Stored action: run a named query and write its rows. The pipeline does not write.
/// </summary>
public class QueryWriteActionDto
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>Named query on the same dataset.</summary>
    public string Query { get; set; } = string.Empty;

    public string TargetDataset { get; set; } = string.Empty;

    public List<string> KeyFields { get; set; } = new();

    /// <summary><c>atla</c> or <c>yenile</c>.</summary>
    public string Mode { get; set; } = "atla";

    /// <summary>Run this action after a row on this dataset is created, updated, or deleted.</summary>
    public bool AfterWrite { get; set; }
}
