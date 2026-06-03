using MediatR;
using Microsoft.Extensions.Options;
using MngReactor.Application.Abstractions.Data;
using MngReactor.Application.Features.Query;
using MngReactor.Persistence.Settings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace MngReactor.Application.Features.Command.Data
{
    public class DataCommandHandler : IRequestHandler<DataCommandRequest, DataCommandResponse>
    {
        private readonly IOptions<MngReactorSettings> _options;
        private readonly IDataProcessing _dataProcessing;

        public DataCommandHandler(IOptions<MngReactorSettings> options, IDataProcessing dataProcessing)
        {
            _options = options;
            _dataProcessing = dataProcessing;
        }

        public async Task<DataCommandResponse> Handle(DataCommandRequest request, CancellationToken cancellationToken)
        {
            JsonNode res = request.Method == DataOperationType.Insert
                ? await _dataProcessing.InsertData(request)
                : request.Method == DataOperationType.Update
                    ? await _dataProcessing.UpdateData(request)
                    : await _dataProcessing.DeleteData(request);

            DataCommandResponse response = new DataCommandResponse
            {
                Data = res,
                ErrorText = string.Empty,
                IsSuccess = res["isSuccess"].GetValue<bool>(),
            };

            return response;
        }
    }
}