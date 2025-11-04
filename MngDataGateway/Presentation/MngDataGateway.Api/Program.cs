using MngDataGateway.Api.Config;
using MngDataGateway.Api.Middleware;
using MngDataGateway.Application;
using MngDataGateway.Application.Configuration;
using MngDataGateway.Infrastructure.Services.Certificate;
using MongoDB.Driver;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Load environment variables
builder.Configuration.AddEnvironmentVariables();

var datagatewaySettings = builder.Configuration.GetSection("MngDataGatewaySettings").Get<MngDataGatewaySettings>();

var log = builder.InitSerilog(datagatewaySettings);

var certificate = CertificateHandler.GetCertificate(log, datagatewaySettings);

builder.InitWebAPP(certificate);
builder.InitOpenApi();
builder.InitAuthentication(datagatewaySettings);

builder.Services.AddApplicationServices(datagatewaySettings);

var app = builder.Build();

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
