using MngKeeper.Api.Config;
using MngKeeper.Application;
using MngKeeper.Application.Configuration;
using MngKeeper.Infrastructure.Services.Certificate;
using Microsoft.Extensions.Logging;
using Serilog;
using System.Security.Cryptography.X509Certificates;

var builder = WebApplication.CreateBuilder(args);

// Load environment variables
builder.Configuration.AddEnvironmentVariables();

// Load settings
var keeperSettings = builder.Configuration.GetSection("MngKeeperSettings").Get<MngKeeperSettings>();
if (keeperSettings == null)
{
    throw new InvalidOperationException("MngKeeperSettings configuration is required!");
}

// Initialize Serilog
var log = builder.InitSerilog();

// Get certificate (if configured)
X509Certificate2? certificate = null;
try
{
    if (!string.IsNullOrEmpty(keeperSettings.CertificateSettings?.MNG_CERT_FILE) ||
        !string.IsNullOrEmpty(keeperSettings.CertificateSettings?.DNS))
    {
        // Create a temporary logger factory for certificate handler
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddSerilog(log));
        var logger = loggerFactory.CreateLogger("CertificateHandler");
        certificate = CertificateHandler.GetCertificate(logger, keeperSettings);
    }
}
catch (Exception ex)
{
    log.Warning(ex, "Certificate loading failed, continuing without custom certificate");
}

// Initialize services
builder.InitWebApp(certificate);
builder.InitOpenApi();

// Add application services
builder.Services.AddApplicationServices(keeperSettings);
builder.Services.AddInfrastructureServices();

var app = builder.Build();

// Configure middleware pipeline
app.UseApplicationSettings(keeperSettings, app.Environment);

try
{
    Log.Information("Starting MngKeeper API");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
