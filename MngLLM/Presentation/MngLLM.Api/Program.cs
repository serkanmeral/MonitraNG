using System.Reflection;
using Asp.Versioning;
using Asp.Versioning.ApiExplorer;
using MngLLM.Api.Config;
using MngLLM.Application;
using MngLLM.Application.Configuration;
using MngLLM.Infrastructure;
using MngLLM.Infrastructure.Services.Certificate;
using Microsoft.Extensions.Options;
using Scalar.AspNetCore;
using Serilog;
using Serilog.Events;
using System.Security.Cryptography.X509Certificates;

var builder = WebApplication.CreateBuilder(args);

// Load configuration
builder.Configuration.AddEnvironmentVariables();

var settings = builder.Configuration.GetSection("MngLLMSettings")
    .Get<MngLLMSettings>();

if (settings == null)
{
    throw new InvalidOperationException("MngLLMSettings configuration is required!");
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

// Log version on startup
var version = Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion?.Split('+')[0] ?? "unknown";
Log.Information("MngLLM Starting. Version {Version}", version);

// Get certificate
X509Certificate2 certificate;
try
{
    certificate = CertificateHandler.GetCertificate(Log.Logger, settings);
    Log.Information("Certificate loaded successfully");
}
catch (Exception ex)
{
    Console.WriteLine($"FATAL ERROR: Failed to load certificate - Application cannot start without valid SSL certificate");
    Console.WriteLine($"Exception: {ex.Message}");
    Console.WriteLine($"Stack Trace: {ex.StackTrace}");
    Log.Fatal(ex, "Failed to load certificate - Application cannot start without valid SSL certificate");
    Console.WriteLine("Press any key to exit...");
    Console.ReadKey();
    throw;
}

try
{
    builder.InitWebAPP(certificate);
}
catch (Exception ex)
{
    Console.WriteLine($"ERROR: Failed to initialize WebApp");
    Console.WriteLine($"Exception: {ex.Message}");
    Console.WriteLine($"Stack Trace: {ex.StackTrace}");
    Log.Fatal(ex, "Failed to initialize WebApp");
    Console.WriteLine("Press any key to exit...");
    Console.ReadKey();
    throw;
}

// Add services
builder.Services.AddControllers();

// API Versioning
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
    options.ApiVersionReader = ApiVersionReader.Combine(
        new QueryStringApiVersionReader("version"),
        new HeaderApiVersionReader("Api-Version"),
        new UrlSegmentApiVersionReader()
    );
})
.AddMvc()
.AddApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});

builder.Services.AddEndpointsApiExplorer();

// Swagger configuration with API versioning support
builder.Services.AddSwaggerGen(options =>
{
    // Use ApiExplorer to discover versioned APIs
    options.CustomSchemaIds(type => type.FullName);
});

// Register Swagger configure options for API versioning
builder.Services.AddTransient<IConfigureOptions<Swashbuckle.AspNetCore.SwaggerGen.SwaggerGenOptions>, SwaggerConfigureOptions>();

builder.Services.AddOpenApi();

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("CorsPolicy", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader()
              .WithExposedHeaders("Content-Disposition");
    });
});

// Authentication
builder.Services.AddAuthentication(settings);

// Application & Infrastructure Services
builder.Services.AddApplicationServices(settings);
builder.Services.AddInfrastructureServices();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    var apiVersionDescriptionProvider = app.Services.GetRequiredService<IApiVersionDescriptionProvider>();
    
    // Swagger with custom route and API versioning support
    app.UseSwagger(c =>
    {
        c.RouteTemplate = "api-docs/{documentName}/swagger.json";
    });
    
    app.UseSwaggerUI(c =>
    {
        // Add Swagger UI endpoints for each API version
        foreach (var description in apiVersionDescriptionProvider.ApiVersionDescriptions.OrderByDescending(d => d.ApiVersion))
        {
            c.SwaggerEndpoint(
                $"/api-docs/{description.GroupName}/swagger.json",
                $"MngLLM API {description.GroupName.ToUpperInvariant()}");
        }
        
        c.RoutePrefix = "swagger";
        c.DocumentTitle = "MngLLM API Documentation";
        c.DisplayRequestDuration();
    });
    
    // Scalar API Reference
    app.MapScalarApiReference(options =>
    {
        options
            .WithTitle("MngLLM API")
            .WithTheme(ScalarTheme.Purple)
            .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient)
            .WithOpenApiRoutePattern("/api-docs/{documentName}/swagger.json");
        
        if (!string.IsNullOrEmpty(settings.OpenApiServerPath))
        {
            options.AddServer(new ScalarServer(settings.OpenApiServerPath, "MngLLM Server"));
        }
    });
    
    app.MapOpenApi();
}

app.UseSerilogRequestLogging();

// HTTPS redirection
app.UseHttpsRedirection();

app.UseCors("CorsPolicy");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

Log.Information("MngLLM API running on {Scheme}://{Host}:{Port}", 
    settings.Server.Scheme, settings.Server.Host, settings.Server.Port);

app.Run();
