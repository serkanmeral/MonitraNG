using MngLLM.Application.Configuration;
using System.Security.Cryptography.X509Certificates;

namespace MngLLM.Api.Config;

public static class Extensions
{
    public static void InitWebAPP(this WebApplicationBuilder builder, X509Certificate2 certificate)
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
                options.ListenAnyIP(serverSettings.Port, _opt =>
                {
                    _opt.UseHttps(httpsOptions =>
                    {
                        httpsOptions.ServerCertificate = certificate;
                    });
                });
            }
            else if (serverSettings.Host == "localhost" || serverSettings.Host == "127.0.0.1")
            {
                options.ListenLocalhost(serverSettings.Port, _opt =>
                {
                    _opt.UseHttps(httpsOptions =>
                    {
                        httpsOptions.ServerCertificate = certificate;
                    });
                });
            }
            else
            {
                // Specific IP address
                options.Listen(System.Net.IPAddress.Parse(serverSettings.Host), serverSettings.Port, _opt =>
                {
                    _opt.UseHttps(httpsOptions =>
                    {
                        httpsOptions.ServerCertificate = certificate;
                    });
                });
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
