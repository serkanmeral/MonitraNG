using MngEngine.Domain.Entities.Asset;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MngEngine.Application.Collector.Common
{
    public record BaseCollectorRequest
    {
        public AssetInfo Asset { get; set; }
        public string? AgentId { get; set; }
    }
}