using MediatR;
using Microsoft.AspNetCore.Http.Connections;
using MngEngine.Application.Collector.WindowsHost;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace MngEngine.Persistence.CollectorHandlers.WindowsHost
{
    public class WindowsHostCollectorHandler : IRequestHandler<WindowsHostCollectorRequest, WindowsHostCollectorResponse>
    {
        public async Task<WindowsHostCollectorResponse> Handle(WindowsHostCollectorRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var conn = request.Asset?.ConnectionInfo;
                var assetId = request.Asset?.Asset_Id ?? "?";
                if (conn == null)
                    throw new InvalidOperationException($"Asset {assetId}: ConnectionInfo eksik.");
                string password = conn.Password ?? "";
                string username = conn.UserName ?? "";
                string computerName = conn.Address ?? "";
                if (string.IsNullOrWhiteSpace(username))
                    throw new InvalidOperationException($"Asset {assetId}: WMI için ConnectionInfo.userName gereklidir.");
                if (string.IsNullOrWhiteSpace(computerName))
                    throw new InvalidOperationException($"Asset {assetId}: WMI için ConnectionInfo.address gereklidir.");

                // Şifreyi güvenli hale getirme
                var securePassword = new System.Security.SecureString();
                foreach (char c in password)
                {
                    securePassword.AppendChar(c);
                }
                var credentials = new System.Management.ConnectionOptions
                {
                    Username = username,
                    Password = password,
                    EnablePrivileges = true,
                };

                // WMI Yönetim Kapsamı
                var scope = new ManagementScope($"\\\\{computerName}\\root\\cimv2", credentials);
                scope.Connect();

                // CPU Kullanımı
                var cpuQuery = new ObjectQuery("select * from Win32_Processor");
                var searcher = new ManagementObjectSearcher(scope, cpuQuery);

                JsonArray cpuValArray = new JsonArray();

                foreach (var obj in searcher.Get())
                {
                    JsonObject cpuAllData = new JsonObject();

                    foreach (var item in obj.Properties)
                    {
                        var val = item.Value != null ? item.Value.ToString() : null;

                        cpuAllData[item.Name] = val;
                    }

                    cpuValArray.Add(cpuAllData);
                }

                JsonSerializerOptions jsonSerializerOptions = new JsonSerializerOptions
                {
                    WriteIndented = true,
                };

                Console.WriteLine("CPU Vals : " + JsonSerializer.Serialize(cpuValArray, jsonSerializerOptions));

                // Bellek Kullanımı
                var memoryQuery = new ObjectQuery("select * from Win32_OperatingSystem");
                searcher = new ManagementObjectSearcher(scope, memoryQuery);

                JsonArray memoryValArray = new JsonArray();

                foreach (var obj in searcher.Get())
                {
                    //Console.WriteLine("FreePhysicalMemory: " + obj["FreePhysicalMemory"]);
                    //Console.WriteLine("TotalVisibleMemorySize: " + obj["TotalVisibleMemorySize"]);
                    JsonObject memoryAllData = new JsonObject();

                    foreach (var item in obj.Properties)
                    {
                        var val = item.Value != null ? item.Value.ToString() : null;

                        memoryAllData[item.Name] = val;
                    }

                    memoryValArray.Add(memoryAllData);
                }

                Console.WriteLine("Memory Vals : " + JsonSerializer.Serialize(memoryValArray, jsonSerializerOptions));

                // Disk Kullanımı
                var diskQuery = new ObjectQuery("select * from Win32_LogicalDisk where DriveType=3");
                searcher = new ManagementObjectSearcher(scope, diskQuery);

                JsonArray diskValArray = new JsonArray();

                foreach (var obj in searcher.Get())
                {
                    //Console.WriteLine("DeviceID: " + obj["DeviceID"]);
                    //Console.WriteLine("FreeSpace: " + obj["FreeSpace"]);
                    //Console.WriteLine("Size: " + obj["Size"]);

                    JsonObject diskAllData = new JsonObject();

                    foreach (var item in obj.Properties)
                    {
                        var val = item.Value != null ? item.Value.ToString() : null;

                        diskAllData[item.Name] = val;
                    }

                    diskValArray.Add(diskAllData);
                }

                Console.WriteLine("Disk Vals : " + JsonSerializer.Serialize(diskValArray, jsonSerializerOptions));
            }
            catch (Exception ex)
            {
                var a = 1;
            }

            return new WindowsHostCollectorResponse
            {
                Result = $"WindowsHost {request.Asset.Asset_Name}"
            };
        }
    }
}