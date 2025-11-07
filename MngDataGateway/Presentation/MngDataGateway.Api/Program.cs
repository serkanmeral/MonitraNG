using MngDataGateway.Api.Config;
using MngDataGateway.Api.Middleware;
using MngDataGateway.Application;
using MngDataGateway.Application.Configuration;
using MngDataGateway.Infrastructure;
using MngDataGateway.Infrastructure.Services.Certificate;
using MngDataGateway.Persistence;
using MongoDB.Driver;
using Serilog;
using System.Security.Cryptography.X509Certificates;

var builder = WebApplication.CreateBuilder(args);

// Load environment variables
builder.Configuration.AddEnvironmentVariables();

var datagatewaySettings = builder.Configuration.GetSection("MngDataGatewaySettings").Get<MngDataGatewaySettings>();
if (datagatewaySettings == null)
{
    throw new InvalidOperationException("MngDataGatewaySettings configuration is required!");
}

var log = builder.InitSerilog(datagatewaySettings);

// Get certificate
X509Certificate2 certificate;
try
{
    certificate = CertificateHandler.GetCertificate(log, datagatewaySettings);
    log.Information("Certificate loaded successfully");
}
catch (Exception ex)
{
    log.Fatal(ex, "Failed to load certificate - Application cannot start without valid SSL certificate");
    throw;
}

builder.InitWebAPP(certificate);
builder.InitOpenApi();
builder.InitAuthentication(datagatewaySettings);

// HttpContextAccessor - MongoContextService için gerekli
builder.Services.AddHttpContextAccessor();

// Application, Infrastructure & Persistence Services
builder.Services.AddApplicationServices(datagatewaySettings);
builder.Services.AddInfrastructureServices();
builder.Services.AddPersistenceServices();

var app = builder.Build();

// Initialize RabbitMQ connection on startup
try
{
    var rabbitMqService = app.Services.GetRequiredService<MngDataGateway.Application.Services.IRabbitMqService>();
    await rabbitMqService.ConnectAsync();
    Log.Information("RabbitMQ connection initialized");
}
catch (Exception ex)
{
    Log.Warning(ex, "Failed to connect to RabbitMQ on startup - will retry on first publish");
}

app.UseApplicationSettings(datagatewaySettings);

try
{
    Log.Information("Starting MngDataGateway API");
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
