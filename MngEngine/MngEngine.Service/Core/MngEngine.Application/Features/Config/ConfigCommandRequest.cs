using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MngEngine.Application.Features.Config
{
    public class ConfigCommandRequest:IRequest<ConfigCommandResponse>
    {
        public string ConfigText { get; set; }
    }
}
