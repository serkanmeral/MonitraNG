using MediatR;
using MngReactor.Application.Abstractions.SecEvents;

namespace MngReactor.Application.Features.Commands.Ingest;

public sealed class SecEventIngestCommandHandler : IRequestHandler<SecEventIngestCommand, SecEventIngestResponse>
{
    private readonly ISecEventIngestProcessing _processing;

    public SecEventIngestCommandHandler(ISecEventIngestProcessing processing)
    {
        _processing = processing;
    }

    public Task<SecEventIngestResponse> Handle(SecEventIngestCommand request, CancellationToken cancellationToken) =>
        _processing.ProcessAsync(
            request.Request,
            request.DomainFromToken,
            request.AccessToken,
            cancellationToken);
}
