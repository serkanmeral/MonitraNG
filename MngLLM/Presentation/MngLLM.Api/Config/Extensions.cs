using MngLLM.Application.Configuration;
using System.Net;

namespace MngLLM.Api.Config;

public static class Extensions
{
    public static void InitWebAPP(this WebApplicationBuilder builder)
    {
        // Get server settings from configuration
        var serverSettings = builder.Configuration.GetSection("MngLLMSettings:Server").Get<ServerSettings>() 
            ?? new ServerSettings();

        builder.WebHost.ConfigureKestrel(options =>
        {
            options.AddServerHeader = false;

            // Parse host - if "0.0.0.0" or "*" listen on any IP
            if (serverSettings.Host == "0.0.0.0" || serverSettings.Host == "*")
            {
                options.ListenAnyIP(serverSettings.Port);
            }
            else if (serverSettings.Host == "localhost" || serverSettings.Host == "127.0.0.1")
            {
                options.ListenLocalhost(serverSettings.Port);
            }
            else
            {
                // Specific IP address
                options.Listen(IPAddress.Parse(serverSettings.Host), serverSettings.Port);
            }

            // Log the configuration
            var logger = builder.Services.BuildServiceProvider().GetService<Serilog.ILogger>();
            logger?.Information($"Kestrel configured to listen on {serverSettings.Host}:{serverSettings.Port} ({serverSettings.Scheme})");
        });

        builder.Services.AddControllers().AddJsonOptions(o =>
        {
            o.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
            o.JsonSerializerOptions.MaxDepth = 64;
        });
    }
}
