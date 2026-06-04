using MediatR;
using MngEngine.Application.Collector.Common;

namespace MngEngine.Application.Collector.SnmpHost;

public record SnmpCollectorRequest : BaseCollectorRequest, IRequest<SnmpCollectorResponse>
{
}
