using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MngReactor.Application.Abstractions.Crypt;
using MngReactor.Application.Abstractions.Data;
using MngReactor.Application.Abstractions.SecEvents;
using MngReactor.Domain.Interfaces;

namespace MngReactor.Tests.Helpers;

/// <summary>
/// connection_info sifreleme testleri icin - CapturingMockDataGatewayClient ve MockCryptProcessing kullanir.
/// ConfigureWebHost ile standart TestServer akisini kullanir.
/// </summary>
public class MngReactorEncryptionTestFactory : WebApplicationFactory<Program>
{
    public CapturingMockDataGatewayClient CapturingClient { get; } = new();

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
            services.AddSingleton(CapturingClient);
            services.AddScoped<IDataGatewayClient>(_ => CapturingClient);

            var cryptDescriptors = services.Where(d => d.ServiceType == typeof(ICryptProcessing)).ToList();
            foreach (var d in cryptDescriptors)
                services.Remove(d);
            services.AddScoped<ICryptProcessing, MockCryptProcessing>();

            var secEventsRepoDescriptors = services.Where(d => d.ServiceType == typeof(ISecEventsRepository)).ToList();
            foreach (var d in secEventsRepoDescriptors)
                services.Remove(d);
            services.AddScoped<ISecEventsRepository, MockSecEventsRepository>();

            var mqttDescriptors = services.Where(d => d.ServiceType == typeof(IMqttService)).ToList();
            foreach (var d in mqttDescriptors)
                services.Remove(d);
            services.AddSingleton<IMqttService, NoopMqttService>();
        });
    }
}
