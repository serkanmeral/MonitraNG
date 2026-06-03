using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace MngReactor.Application.Abstractions.Domain
{
    public interface IDomainProcessing
    {
        Task<JsonArray> GetDomainAsync(string domainName);

        Task<JsonArray> GetAllDomainsAsync();
    }
}