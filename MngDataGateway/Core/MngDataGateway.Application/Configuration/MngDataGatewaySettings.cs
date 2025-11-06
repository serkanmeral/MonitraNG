using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MngDataGateway.Application.Configuration
{

    public class MngDataGatewaySettings
    {
        public ServerSettings Server { get; set; }
        public Mongodb MongoDB { get; set; }
        public Rabbitmq RabbitMQ { get; set; }
        public CertificateSettings CertificateSettings { get; set; }
        public string OpenApiServerPath { get; set; }
        public Actors Actors { get; set; }
    }

    public class ServerSettings
    {
        public string Host { get; set; } = "0.0.0.0";
        public int Port { get; set; } = 5010;
        public string Scheme { get; set; } = "https";
    }
    public class CertificateSettings
    {
        public string DNS { get; set; }
        public string MNG_CERT_FILE { get; set; }
        public string MNG_KEY_FILE { get; set; }
        public string MNG_CERT_FILE_CONTENT { get; set; }
        public string MNG_KEY_FILE_CONTENT { get; set; }

    }

    public class Actors
    {
        public string MngKeeper { get; set; }
    }
    public class Mongodb
    {
        public string ConnectionString { get; set; }
    }

    public class Rabbitmq
    {
        public string Host { get; set; }
        public int Port { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string VirtualHost { get; set; }
    }

}
