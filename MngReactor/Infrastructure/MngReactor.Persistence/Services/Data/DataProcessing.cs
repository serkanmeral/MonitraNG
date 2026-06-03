using MngReactor.Application.Abstractions.Data;
using MngReactor.Application.Features.Command.Data;
using MngReactor.Application.Features.Query;
using MngReactor.Application.Repositories.Data;
using MngReactor.Domain.Interfaces;
using MngReactor.Persistence.Repositories.Data;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace MngReactor.Persistence.Services.Data
{
    public class DataProcessing : IDataProcessing
    {
        private readonly IDataRepository _dataRepository;
        private readonly IMqttService _mqttService;

        public DataProcessing(IDataRepository dataRepository, IMqttService mqttService)
        {
            _dataRepository = dataRepository;
            _mqttService = mqttService;
        }

        public async Task<JsonNode> DeleteData(DataCommandRequest request)
        {
            var res = await _dataRepository.DeleteData(request);

            return res;
        }

        //public async Task<JsonNode> GetData(MongoDbConnectionInfo connectionInfo, string collectionName, JsonNode query)
        //{
        //    var res = await _dataRepository.GetData(connectionInfo, collectionName, query);

        //    return res;
        //}

        public async Task<JsonNode> GetData(GetDataQueryRequest request)
        {
            var res = await _dataRepository.GetData(request);

            return res;
        }

        public async Task<JsonNode> InsertData(DataCommandRequest request)
        {
            var res = await _dataRepository.InsertData(request);

            if (res["isSuccess"].GetValue<bool>())
            {
                if (request.PublishMQTT)
                {
                    _mqttService.PublishAsync(@"MNG/" + request.Domain + @"/"+request.Collection, JsonSerializer.Serialize(res));
                }
            }

            return res;
        }

        public async Task<JsonNode> UpdateData(DataCommandRequest request)
        {
            var res = await _dataRepository.UpdateData(request);

            return res;
        }
    }
}