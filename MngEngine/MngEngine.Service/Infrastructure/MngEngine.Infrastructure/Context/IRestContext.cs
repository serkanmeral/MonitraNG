using RestSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MngEngine.Infrastructure.Context
{
    public interface IRestContext
    {
        RestClient RestClient(string path);
    }
}
