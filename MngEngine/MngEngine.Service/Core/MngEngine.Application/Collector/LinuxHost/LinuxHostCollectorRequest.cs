using MediatR;
using MngEngine.Application.Collector.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace MngEngine.Application.Collector.LinuxHost
{
    public record LinuxHostCollectorRequest : BaseCollectorRequest, IRequest<LinuxHostCollectorResponse>
    {
    }
}