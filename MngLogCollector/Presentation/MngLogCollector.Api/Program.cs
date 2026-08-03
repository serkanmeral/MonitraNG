using Asp.Versioning;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using MngLogCollector.Application.Configuration;
using MngLogCollector.Infrastructure;
using Serilog;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddEnvironmentVariables();

builder.Services.Configure<MngLogCollectorSettings>(
    builder.Configuration.GetSection(MngLogCollectorSettings.SectionName));

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();
builder.Host.UseSerilog();

builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        // UI multi-selects occasionally emit numeric IDs as JSON strings.
        o.JsonSerializerOptions.NumberHandling =
            System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString;
    });

builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
}).AddMvc().AddApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c => c.SwaggerDoc("v1", new() { Title = "MngLogCollector API", Version = "v1" }));

var jwtAuthority = builder.Configuration["Jwt:Authority"];
if (!string.IsNullOrEmpty(jwtAuthority))
{
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.Authority = jwtAuthority;
            options.RequireHttpsMetadata = false;
            options.TokenValidationParameters = new() { ValidateIssuer = false, ValidateAudience = false };
        });
}

builder.Services.AddInfrastructureServices();

var app = builder.Build();
app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("EnableSwagger"))
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("v1/swagger.json", "MngLogCollector API v1"));
}

if (!string.IsNullOrEmpty(jwtAuthority))
{
    app.UseAuthentication();
    app.UseAuthorization();
}

app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "mnglogcollector" }));
app.MapGet("/api/v1/health", () => Results.Ok(new { status = "healthy", service = "mnglogcollector" }));
app.MapControllers();

Log.Information("Starting MngLogCollector API (field MngLogs agents connect here)");
app.Run();

public partial class Program;
