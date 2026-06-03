using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using MngReactor.Application.Abstractions.Data;
using MngReactor.Persistence.Settings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace MngReactor.Application.Features.Query
{
    public class GetDataQueryHandler : IRequestHandler<GetDataQueryRequest, GetDataQueryResponse>
    {
        private readonly IDataProcessing _dataProcessing;
        private readonly IOptions<MngReactorSettings> _options;

        public GetDataQueryHandler(IDataProcessing dataProcessing, IOptions<MngReactorSettings> options)
        {
            _dataProcessing = dataProcessing;
            _options = options;
        }

        public async Task<GetDataQueryResponse> Handle(GetDataQueryRequest request, CancellationToken cancellationToken)
        {
            var res = await _dataProcessing.GetData(request);

            GetDataQueryResponse response = new()
            {
                IsSuccess = true,
                ErrorText = string.Empty,
                Data = res
            };

            return response;
        }
    }
}