using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace MngReactor.Application.Features.Query
{
    public record GetDataQueryResponse
    {
        public bool IsSuccess { get; set; }
        public string ErrorText { get; set; }
        public JsonNode Data { get; set; }
    }
}