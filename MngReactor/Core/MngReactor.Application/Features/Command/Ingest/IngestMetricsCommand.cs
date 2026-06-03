using MediatR;

namespace MngReactor.Application.Features.Commands.Ingest;

public record IngestMetricsCommand(IngestMetricsRequest Request, string DomainFromToken, string? AccessToken = null) : IRequest<IngestMetricsResponse>;
