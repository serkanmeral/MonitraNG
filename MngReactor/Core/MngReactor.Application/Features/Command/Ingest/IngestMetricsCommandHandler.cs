using MediatR;
using MngReactor.Application.Abstractions.Ingest;

namespace MngReactor.Application.Features.Commands.Ingest;

public class IngestMetricsCommandHandler : IRequestHandler<IngestMetricsCommand, IngestMetricsResponse>
{
    private readonly IIngestProcessing _ingestProcessing;

    public IngestMetricsCommandHandler(IIngestProcessing ingestProcessing)
    {
        _ingestProcessing = ingestProcessing;
    }

    public Task<IngestMetricsResponse> Handle(IngestMetricsCommand request, CancellationToken cancellationToken)
    {
        return _ingestProcessing.ProcessAsync(request.Request, request.DomainFromToken, request.AccessToken, cancellationToken);
    }
}
