namespace MngDataGateway.Application.DTOs.Data;

/// <summary>
/// Result of a stored query-write action. Earlier rows stay when a later row fails.
/// </summary>
public class QueryWriteActionResultDto
{
    public int Written { get; set; }

    public int Skipped { get; set; }

    public int Failed { get; set; }

    public List<string> Errors { get; set; } = new();
}
