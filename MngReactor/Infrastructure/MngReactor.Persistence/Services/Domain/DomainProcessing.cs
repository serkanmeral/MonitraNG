using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using MngReactor.Application.Abstractions.Data;
using MngReactor.Application.Abstractions.Domain;
using MngReactor.Application.Features.Query;
using MngReactor.Persistence.Settings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace MngReactor.Persistence.Services.Domain
{
    public class DomainProcessing : IDomainProcessing
    {
        private readonly IDataProcessing _dataProcessing;
        private readonly IOptions<MngReactorSettings> _options;

        public DomainProcessing(IDataProcessing dataProcessing, IOptions<MngReactorSettings> options)
        {
            _dataProcessing = dataProcessing;
            _options = options;
        }

        private async Task<JsonArray> BuildDomainQueryAsync(JsonObject query)
        {
            //MongoDbConnectionInfo connectionInfo = new MongoDbConnectionInfo
            //{
            //    Database = "mng_owners",
            //    Host = _options.Value.MongoPath.host,
            //    Port = _options.Value.MongoPath.port,
            //    Password = _options.Value.MongoPath.password,
            //    UserName = _options.Value.MongoPath.username
            //};

            GetDataQueryRequest request = new GetDataQueryRequest
            {
                Access_Token = string.Empty,
                Collection = "owner_data",
                Database = "mng_owners",
                Query = query
            };



            //var res = await _dataProcessing.GetData(connectionInfo, "owner_data", query);
            var res = await _dataProcessing.GetData(request);

            return (JsonArray)res;
        }

        public async Task<JsonArray> GetAllDomainsAsync()
        {
            return await BuildDomainQueryAsync(new JsonObject());
        }

        public async Task<JsonArray> GetDomainAsync(string domainName)
        {
            JsonObject query = new JsonObject();
            query["subDomain"] = domainName;

            return await BuildDomainQueryAsync(query);
        }
    }
}