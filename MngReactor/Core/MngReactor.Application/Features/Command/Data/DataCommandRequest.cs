using MediatR;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace MngReactor.Application.Features.Command.Data
{
    public enum DataOperationType
    {
        Insert = 0, Update = 1, Delete = 2
    }
    public record DataCommandOptions
    {
        public bool useCreatedBy { get; set; }
        public bool useUpdatedBy { get; set; }
    }

    public record DataCommandRequest : IRequest<DataCommandResponse>
    {
        public string Database { get; set; }
        public string Collection { get; set; }
        public DataOperationType Method { get; set; }
        public string? Access_Token { get; set; }
        public JsonNode Data { get; set; }
        public DataCommandOptions Options { get; set; }
        public string UserName { get; set; }
        public bool PublishMQTT { get; set; }
        public string Domain { get; set; }

    }
}