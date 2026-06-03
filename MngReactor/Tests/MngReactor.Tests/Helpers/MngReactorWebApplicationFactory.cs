using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MngReactor.Application.Abstractions.Data;
using MngReactor.Application.Abstractions.Ingest;
using MngReactor.Domain.Interfaces;

namespace MngReactor.Tests.Helpers;

/// <summary>
/// WebApplicationFactory - standart ConfigureWebHost ile TestServer kullanir.
/// ContentRoot otomatik olarak API projesine ayarlanir.
/// </summary>
public class MngReactorWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test");
        builder.UseTestServer();

        var testConfig = new Dictionary<string, string?>
        {
            ["MngReactorSettings:Server:Host"] = "127.0.0.1",
            ["MngReactorSettings:Server:Port"] = "0",
            ["MngReactorSettings:Mqtt:Host"] = "",
            ["MngReactorSettings:Actors:MngKeeper"] = "http://localhost:5001",
            ["MngReactorSettings:DataGateway:BaseUrl"] = "http://localhost:5010",
            ["MngReactorSettings:DataGateway:ApiVersion"] = "v1",
            ["MngReactorSettings:Crypt:IngestDecryptKey"] = "0123456789abcdef",
            ["MngReactorSettings:Crypt:IngestEncryptKey"] = "abcdef0123456789",
            ["Serilog:MinimumLevel:Default"] = "Warning",
            ["Serilog:WriteTo:0:Name"] = "Console"
        };
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(testConfig));

        builder.ConfigureTestServices(services =>
        {
            services.AddAuthentication(TestAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });

            var dgDescriptors = services.Where(d => d.ServiceType == typeof(IDataGatewayClient)).ToList();
            foreach (var d in dgDescriptors)
                services.Remove(d);
            services.AddScoped<IDataGatewayClient, MockDataGatewayClient>();

            var metricsRepoDescriptors = services.Where(d => d.ServiceType == typeof(IMonMetricsRepository)).ToList();
            foreach (var d in metricsRepoDescriptors)
                services.Remove(d);
            services.AddScoped<IMonMetricsRepository, MockMonMetricsRepository>();

            var mqttDescriptors = services.Where(d => d.ServiceType == typeof(IMqttService)).ToList();
            foreach (var d in mqttDescriptors)
                services.Remove(d);
            services.AddSingleton<IMqttService, NoopMqttService>();
        });
    }
}
