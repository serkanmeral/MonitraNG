using MngKeeper.Api.Config;
using MngKeeper.Application;
using MngKeeper.Application.Configuration;
using MngKeeper.Infrastructure.Services.Certificate;
using Microsoft.Extensions.Logging;
using Serilog;
using System.Security.Cryptography;
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

// Get certificate
X509Certificate2 certificate;
try
{
    certificate = CertificateHandler.GetCertificate(log, keeperSettings);
    log.Information("Certificate loaded successfully");
}
catch (Exception ex)
{
    log.Fatal(ex, "Failed to load certificate - Application cannot start without valid SSL certificate");
    throw;
}

// Initialize services
builder.InitWebAPP(certificate!);
builder.InitOpenApi();

// Add application services
builder.Services.AddApplicationServices(keeperSettings);
builder.Services.AddInfrastructureServices();

var app = builder.Build();

// Configure middleware pipeline
app.UseApplicationSettings(keeperSettings, app.Environment);

// Configure Seq retention policies (non-blocking, runs in background)
_ = Task.Run(async () =>
{
    try
    {
        await Task.Delay(2000); // Wait 2 seconds for Seq to be ready
        await app.ConfigureSeqRetentionPoliciesAsync(builder.Configuration);
    }
    catch (Exception ex)
    {
        Log.Warning(ex, "Failed to configure Seq retention policies");
    }
});

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
