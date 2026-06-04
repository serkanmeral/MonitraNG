namespace MngReactor.Application.Features.Commands.Ingest;

public record SecEventIngestResponse
{
    public int Accepted { get; init; }
    public int Rejected { get; init; }
    public int Published { get; init; }
    public bool ImplementationPending { get; init; }
    public string? Message { get; init; }
}
