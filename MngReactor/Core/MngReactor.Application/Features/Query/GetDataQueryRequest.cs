using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace MngReactor.Application.Features.Query
{
    //public enum MergeOptions
    //{
    //    full = 1,
    //    none = 0
    //}

    public class GetDataQueryRequest : IRequest<GetDataQueryResponse>
    {
        public string Database { get; set; }
        public string Collection { get; set; }
        public JsonObject? Query { get; set; }

        //public JsonArray? CustomQuery { get; set; }
        public string? Access_Token { get; set; }

        //public string? Id { get; set; }
        //public MergeOptions MergeOptions { get; set; }
        //public int? PageCount { get; set; }
        //public int? PageNumber { get; set; }
        //public string? Sort { get; set; }
        //public string? Filter { get; set; }
        //public string? AnonymousPath { get; set; }
    }
}