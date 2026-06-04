using MediatR;

namespace MngReactor.Application.Features.Commands.Ingest;

public record SecEventIngestCommand(
    SecEventIngestRequest Request,
    string DomainFromToken,
    string? AccessToken = null) : IRequest<SecEventIngestResponse>;
