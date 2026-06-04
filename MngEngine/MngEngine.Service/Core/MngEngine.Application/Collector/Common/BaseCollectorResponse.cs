using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MngEngine.Application.Collector.Common
{
    public record BaseCollectorResponse
    {
        public dynamic Result { get; set; }
    }
}