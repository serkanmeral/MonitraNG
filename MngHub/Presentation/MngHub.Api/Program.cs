using System.Reflection;
using MngHub.Application;
using MngHub.Application.Configuration;
using MngHub.Application.Services;
using MngHub.Infrastructure;
using MngHub.Infrastructure.Services.SignalR;
using Microsoft.Extensions.FileProviders;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);

// Load configuration
builder.Configuration.AddEnvironmentVariables();

var settings = builder.Configuration.GetSection("MngHubSettings")
    .Get<MngHubSettings>();

if (settings == null)
{
    throw new InvalidOperationException("MngHubSettings configuration is required!");
}

// Initialize Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.Debug()
    .CreateLogger();

builder.Host.UseSerilog();

// Configure Kestrel
builder.WebHost.ConfigureKestrel(options =>
{
    // Listen on HTTP port (development)
    options.ListenAnyIP(settings.Server.Port);

    // SignalR connection limits
    options.Limits.MaxConcurrentConnections = settings.Connection.MaxConcurrentConnections;
    options.Limits.MaxConcurrentUpgradedConnections = settings.Connection.MaxConcurrentConnections;
    options.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(2);
});

// Add services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

// SignalR
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
    options.KeepAliveInterval = settings.SignalR.KeepAliveInterval;
    options.ClientTimeoutInterval = settings.SignalR.ClientTimeoutInterval;
    options.HandshakeTimeout = settings.SignalR.HandshakeTimeout;
    options.MaximumReceiveMessageSize = settings.SignalR.MaximumReceiveMessageSize;
});

// CORS (if needed for frontend)
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader()
              .WithExposedHeaders("*"); // SignalR için gerekli
    });
});

// Application & Infrastructure Services
try
{
    builder.Services.AddApplicationServices(settings);
    builder.Services.AddInfrastructureServices();
    
    Log.Information("Services registered successfully");
}
catch (ReflectionTypeLoadException ex)
{
    Log.Fatal(ex, "Failed to load types during service registration");
    foreach (var loaderEx in ex.LoaderExceptions ?? Array.Empty<Exception>())
    {
        Log.Fatal(loaderEx, "Loader exception: {Message}", loaderEx?.Message);
    }
    throw;
}
catch (Exception ex)
{
    Log.Fatal(ex, "Failed to register services");
    throw;
}

WebApplication app;
try
{
    app = builder.Build();
    Log.Information("Application built successfully");
}
catch (ReflectionTypeLoadException ex)
{
    Log.Fatal(ex, "Failed to build application - ReflectionTypeLoadException");
    Log.Fatal("Types that could not be loaded:");
    foreach (var loaderEx in ex.LoaderExceptions ?? Array.Empty<Exception>())
    {
        Log.Fatal(loaderEx, "  - {Message}", loaderEx?.Message);
    }
    throw;
}
catch (Exception ex)
{
    Log.Fatal(ex, "Failed to build application");
    throw;
}

// Initialize RabbitMQ connection on startup
try
{
    var rabbitMqConsumer = app.Services.GetRequiredService<IRabbitMqConsumer>();
    await rabbitMqConsumer.ConnectAsync();
    Log.Information("RabbitMQ connection initialized");
}
catch (Exception ex)
{
    Log.Warning(ex, "Failed to connect to RabbitMQ on startup - will retry on first subscription");
}

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Static files (for test HTML page)
var testsPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "tests");
if (Directory.Exists(testsPath))
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(testsPath),
        RequestPath = "/tests"
    });
    
    Log.Information("Static files enabled for tests directory: {Path}", testsPath);
}

// app.UseHttpsRedirection(); // Disabled for development
app.UseCors(); // CORS must be before UseRouting and MapHub
app.UseRouting();
app.UseAuthorization();
app.MapControllers();

// SignalR Hub endpoint
app.MapHub<NotificationHub>("/ws");

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "MngHub", timestamp = DateTime.UtcNow }))
    .WithName("HealthCheck");

// Test page redirect (for easy access)
app.MapGet("/test", () => Results.Redirect("/tests/test-signalr.html"))
    .WithName("TestPage");

try
{
    Log.Information("Starting MngHub API on {Host}:{Port}", settings.Server.Host, settings.Server.Port);
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
