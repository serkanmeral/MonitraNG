using MediatR;
using MngEngine.Application.Collector.Common;

namespace MngEngine.Application.Collector.HttpHost;

public record HttpCollectorRequest : BaseCollectorRequest, IRequest<HttpCollectorResponse>
{
    public required HttpConnectionInfo HttpConnectionInfo { get; init; }
}
