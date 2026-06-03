using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MngReactor.Application.Features.Engine.Assets
{
    public class GetEngineAssetsQueryRequest:IRequest<GetEngineAssetsQueryResponse>
    {
        public dynamic UserInfo { get; set; }
        public string EngineId { get; set; }
    }
}
