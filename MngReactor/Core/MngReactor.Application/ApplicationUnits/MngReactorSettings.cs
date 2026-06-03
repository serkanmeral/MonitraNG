using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MngReactor.Persistence.Settings
{
    public class MngReactorSettings
    {
        public Mongopath MongoPath { get; set; }
        public string SeqPath { get; set; }
        public string ClientName { get; set; }
        public string Password { get; set; }
        public string TokenService { get; set; }
        public string CompressPrk { get; set; }
        public string CompressPbk { get; set; }
        public MqttSettings MqttSettings { get; set; }
        public int ApplicationPort { get; set; }
        /// <summary>
        /// mon_metrics TTL (gün). Varsayılan 90.
        /// </summary>
        public int MetricsTtlDays { get; set; } = 90;
    }

    public class MqttSettings
    {
        public string UserName { get; set; }
        public string Password { get; set; }
        public string Host { get; set; }
        public int Port { get; set; }
    }

    public class Mongopath
    {
        public string host { get; set; }
        public int port { get; set; }
        public string username { get; set; }
        public string password { get; set; }
    }
}