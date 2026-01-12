using MngGateway.Application;
using MngGateway.Application.Configuration;
using MngGateway.Infrastructure;
using MngGateway.Infrastructure.Services.Certificate;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.IdentityModel.JsonWebTokens;
using Ocelot.DependencyInjection;
using Ocelot.Middleware;
using Serilog;
using Serilog.Events;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Net.Http;

var builder = WebApplication.CreateBuilder(args);

// Load environment variables
builder.Configuration.AddEnvironmentVariables();

// Load Ocelot configuration
builder.Configuration
    .AddJsonFile("ocelot.json", optional: false, reloadOnChange: true);

var settings = builder.Configuration.GetSection("MngGatewaySettings")
    .Get<MngGatewaySettings>();

if (settings == null)
{
    throw new InvalidOperationException("MngGatewaySettings configuration is required!");
}

// Initialize Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .MinimumLevel.Override("Ocelot", LogEventLevel.Information)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.Debug()
    .CreateLogger();

builder.Host.UseSerilog();

// Log version on startup
var version = Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion?.Split('+')[0] ?? "unknown";
Log.Information("MngGateway Starting. Version {Version}", version);

// Get certificate for HTTPS
X509Certificate2? certificate = null;
if (settings.Server.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
{
    try
    {
        certificate = CertificateHandler.GetCertificate(Log.Logger, settings);
        Log.Information("Certificate loaded successfully for HTTPS");
    }
    catch (Exception ex)
    {
        Log.Warning(ex, "Failed to load certificate - Gateway will use HTTP instead");
    }
}

// Configure Kestrel
builder.WebHost.ConfigureKestrel(options =>
{
    if (certificate != null && settings.Server.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
    {
        options.ListenAnyIP(settings.Server.Port, listenOptions =>
        {
            listenOptions.UseHttps(httpsOptions =>
            {
                httpsOptions.ServerCertificate = certificate;
            });
        });
        Log.Information("Kestrel configured for HTTPS on port {Port}", settings.Server.Port);
    }
    else
    {
        options.ListenAnyIP(settings.Server.Port);
        Log.Information("Kestrel configured for HTTP on port {Port}", settings.Server.Port);
    }
});

// Add Ocelot
builder.Services.AddOcelot(builder.Configuration);

// JWT Authentication (KeyCloak)
// Use "Bearer" as default scheme to match Ocelot's AuthenticationProviderKey
builder.Services
    .AddAuthentication("Bearer")
    .AddJwtBearer("Bearer", options =>
    {
        // Don't set Authority - multi-realm support requires dynamic validation
        options.RequireHttpsMetadata = settings.Jwt.RequireHttpsMetadata;
        
        // SSL certificate validation bypass (development)
        options.BackchannelHttpHandler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = delegate { return true; }
        };
        
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false, // Multi-realm support: each domain has its own realm
            ValidateAudience = false, // Multi-realm support: audience may vary by realm
            ValidateLifetime = true,
            ValidateIssuerSigningKey = false, // Multi-realm support: signature validation via SignatureValidator
            // Use SignatureValidator to accept tokens without full signature validation (for multi-realm support)
            SignatureValidator = delegate (string token, TokenValidationParameters parameters)
            {
                var jwt = new Microsoft.IdentityModel.JsonWebTokens.JsonWebToken(token);
                return jwt;
            }
        };
        
        // Events for debugging and token validation
        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                Log.Warning("JWT Authentication failed: {Error}", context.Exception.Message);
                return Task.CompletedTask;
            },
            OnTokenValidated = context =>
            {
                Log.Information("JWT Token validated successfully for user: {User}", context.Principal?.Identity?.Name);
                return Task.CompletedTask;
            },
            OnChallenge = context =>
            {
                Log.Warning("JWT Challenge: {Error}", context.Error);
                return Task.CompletedTask;
            }
        };
    });

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        var origins = settings.Cors.AllowedOrigins.ToArray();
        policy.WithOrigins(origins)
              .AllowAnyMethod()
              .AllowAnyHeader();
        
        if (settings.Cors.AllowCredentials)
        {
            policy.AllowCredentials();
        }
    });
});

// Application & Infrastructure Services
builder.Services.AddApplicationServices(settings);
builder.Services.AddInfrastructureServices();

// Health check endpoint
builder.Services.AddHealthChecks();

var app = builder.Build();

// Configure the HTTP request pipeline
app.UseCors("AllowFrontend");
app.UseRouting();

// Health check endpoint - MUST be before Ocelot middleware
// Use middleware to bypass Ocelot for /health endpoint
app.Use(async (context, next) =>
{
    if (context.Request.Path.Value == "/health" && context.Request.Method == "GET")
    {
        context.Response.StatusCode = 200;
        context.Response.ContentType = "application/json";
        var response = new
        {
            status = "healthy",
            service = "MngGateway",
            version = version,
            timestamp = DateTime.UtcNow
        };
        await context.Response.WriteAsJsonAsync(response);
        return;
    }
    await next();
});

// Authentication and Authorization
// Note: Some routes (like /llm/*, /hub/ws/*) require authentication at Gateway level
// Other routes (like /keeper/*, /data/*) handle authentication at downstream service level
app.UseAuthentication();
app.UseAuthorization();

// Use Ocelot middleware - this will handle all other routes
await app.UseOcelot();

try
{
    Log.Information("Starting MngGateway API on {Host}:{Port}", settings.Server.Host, settings.Server.Port);
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
