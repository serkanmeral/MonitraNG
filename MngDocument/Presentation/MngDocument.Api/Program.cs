using MngDocument.Api.Config;
using MngDocument.Api.Services;
using MngDocument.Application;
using MngDocument.Application.Configuration;
using MngDocument.Application.Interfaces;
using MngDocument.Infrastructure;
using Serilog;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddEnvironmentVariables();

var settings = builder.Configuration.GetSection(MngDocumentSettings.SectionName).Get<MngDocumentSettings>()
    ?? throw new InvalidOperationException($"{MngDocumentSettings.SectionName} configuration is required.");

builder.InitSerilog();
builder.InitWebApp(settings);
builder.InitOpenApi();
builder.InitAuthentication();

builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new Asp.Versioning.ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
    options.ApiVersionReader = Asp.Versioning.ApiVersionReader.Combine(
        new Asp.Versioning.QueryStringApiVersionReader("version"),
        new Asp.Versioning.HeaderApiVersionReader("Api-Version"),
        new Asp.Versioning.UrlSegmentApiVersionReader());
})
.AddMvc()
.AddApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient();
builder.Services.AddScoped<IRequestContext, HttpRequestContext>();

builder.Services.AddApplicationServices(settings);
builder.Services.AddInfrastructureServices(settings);

var app = builder.Build();
app.UseApplicationPipeline();

Log.Information("MngDocument API listening on {Host}:{Port}", settings.Server.Host, settings.Server.Port);
app.Run();
