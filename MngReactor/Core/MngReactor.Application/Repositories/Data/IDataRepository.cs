using MngReactor.Application.Features.Command.Data;
using MngReactor.Application.Features.Query;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace MngReactor.Application.Repositories.Data
{
    public interface IDataRepository
    {
        Task<JsonNode> GetData(GetDataQueryRequest request);

        Task<JsonNode> InsertData(DataCommandRequest request);

        Task<JsonNode> UpdateData(DataCommandRequest request);

        Task<JsonNode> DeleteData(DataCommandRequest request);
    }
}