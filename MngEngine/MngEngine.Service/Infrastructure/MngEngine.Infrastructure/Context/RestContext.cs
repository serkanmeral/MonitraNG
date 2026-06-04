using RestSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MngEngine.Infrastructure.Context
{
    public class RestContext : IRestContext
    {
        public RestContext()
        {

        }

        public RestClient RestClient(string path)
        {

            var options = new RestClientOptions(path)
            {
                RemoteCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true
            };

            var restClient = new RestClient(options);

            return restClient;

        }
    }
}
