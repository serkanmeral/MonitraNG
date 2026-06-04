using MediatR;
using MngEngine.Application.Collector.LinuxHost;
using Renci.SshNet;

namespace MngEngine.Persistence.CollectorHandlers.WindowsHost
{
    public class LinuxHostCollectorHandler : IRequestHandler<LinuxHostCollectorRequest, LinuxHostCollectorResponse>
    {
        private static void ParseVmstatOutput(string output)
        {
            var lines = output.Split('\n');

            if (lines.Length < 3)
            {
                Console.WriteLine("Invalid vmstat output");
                return;
            }

            // Ignore the first two lines (headers)
            var header1 = lines[0];
            var header2 = lines[1];
            var dataLine = lines[2];

            var data = dataLine.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            if (data.Length != 17)
            {
                Console.WriteLine("Unexpected vmstat data length");
                return;
            }

            var vmstatData = new
            {
                Procs_r = int.Parse(data[0]),
                Procs_b = int.Parse(data[1]),
                Memory_swpd = int.Parse(data[2]),
                Memory_free = int.Parse(data[3]),
                Memory_buff = int.Parse(data[4]),
                Memory_cache = int.Parse(data[5]),
                Swap_si = int.Parse(data[6]),
                Swap_so = int.Parse(data[7]),
                IO_bi = int.Parse(data[8]),
                IO_bo = int.Parse(data[9]),
                System_in = int.Parse(data[10]),
                System_cs = int.Parse(data[11]),
                CPU_us = int.Parse(data[12]),
                CPU_sy = int.Parse(data[13]),
                CPU_id = int.Parse(data[14]),
                CPU_wa = int.Parse(data[15]),
                CPU_st = int.Parse(data[16]),
            };

            Console.WriteLine("Parsed vmstat data:");
            Console.WriteLine(vmstatData);
        }

        private static void ParsePsOutput(string output)
        {
            var lines = output.Split('\n');
            foreach (var line in lines)
            {
                Console.WriteLine(line);
            }
        }

        private static void ParseSystemctlOutput(string output)
        {
            var lines = output.Split('\n');
            foreach (var line in lines)
            {
                Console.WriteLine(line);
            }
        }

        public async Task<LinuxHostCollectorResponse> Handle(LinuxHostCollectorRequest request, CancellationToken cancellationToken)
        {
            var conn = request.Asset?.ConnectionInfo;
            var assetId = request.Asset?.Asset_Id ?? "?";
            if (conn == null)
                throw new InvalidOperationException($"Asset {assetId}: ConnectionInfo eksik.");
            var host = conn.Address ?? "";
            var username = conn.UserName ?? "";
            var password = conn.Password ?? "";
            if (string.IsNullOrWhiteSpace(username))
                throw new InvalidOperationException($"Asset {assetId}: SSH için ConnectionInfo.userName gereklidir. Config sync'te bu asset için kullanıcı adı tanımlanmalı.");
            if (string.IsNullOrWhiteSpace(host))
                throw new InvalidOperationException($"Asset {assetId}: SSH için ConnectionInfo.address gereklidir. Config sync'te bu asset için host tanımlanmalı.");

            using (var client = new SshClient(host, username, password))
            {
                client.Connect();

                var cmd = client.CreateCommand("vmstat");
                var resStats = cmd.Execute();

                cmd = client.CreateCommand("ps aux");
                var resApps = cmd.Execute();

                // Tüm servislerin durumunu alma
                cmd = client.CreateCommand("systemctl list-units --type=service");
                var resServices = cmd.Execute();

                client.Disconnect();

                //ParseVmstatOutput(resStats);
                //ParsePsOutput(resApps);
                //ParseSystemctlOutput(resServices);
            }

            return new LinuxHostCollectorResponse
            {
                Result = $"LinuxHost {request.Asset.Asset_Name}"
            };
        }
    }
}