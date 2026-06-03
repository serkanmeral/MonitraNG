//using Amazon.Runtime.Internal;
using MediatR;
using MngReactor.Application.Abstractions.Engine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace MngReactor.Application.Features.Engine.Assets
{
    public class GetEngineAssetsQueryHandler : IRequestHandler<GetEngineAssetsQueryRequest, GetEngineAssetsQueryResponse>
    {
        private readonly IEngineProcessing _engineProcessing;

        public GetEngineAssetsQueryHandler(IEngineProcessing engineProcessing)
        {
            _engineProcessing = engineProcessing;
        }

        public async Task<GetEngineAssetsQueryResponse> Handle(GetEngineAssetsQueryRequest request, CancellationToken cancellationToken)
        {

            var res = await _engineProcessing.GetEngineAssetData(request);

            GetEngineAssetsQueryResponse response = new GetEngineAssetsQueryResponse
            {
                Data = res,
                ErrorText = string.Empty,
                IsSuccess = true

            };

            return response;
        }
    }
}
