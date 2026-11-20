using Asp.Versioning;
using MngExtensions.Api.Config;
using MngExtensions.Application.Configuration;
using MngExtensions.Infrastructure;
using MngExtensions.Persistence;
using Serilog;

try
{
    var builder = WebApplication.CreateBuilder(args);
    builder.Configuration.AddEnvironmentVariables();

    var settings = builder.Configuration.GetSection(MngExtensionsSettings.SectionName).Get<MngExtensionsSettings>()
        ?? throw new InvalidOperationException("MngExtensionsSettings configuration is required.");
    builder.Services.Configure<MngExtensionsSettings>(builder.Configuration.GetSection(MngExtensionsSettings.SectionName));

    builder.InitSerilog();
    builder.InitWebApp(settings);

    builder.Services.AddApiVersioning(options =>
    {
        options.DefaultApiVersion = new ApiVersion(1, 0);
        options.AssumeDefaultVersionWhenUnspecified = true;
        options.ReportApiVersions = true;
        options.ApiVersionReader = ApiVersionReader.Combine(
            new QueryStringApiVersionReader("version"),
            new HeaderApiVersionReader("Api-Version"),
            new UrlSegmentApiVersionReader());
    }).AddMvc().AddApiExplorer(options =>
    {
        options.GroupNameFormat = "'v'VVV";
        options.SubstituteApiVersionInUrl = true;
    });

    builder.InitOpenApi();
    builder.InitAuthentication();
    builder.Services.AddInfrastructureServices();
    builder.Services.AddPersistenceServices();

    var app = builder.Build();
    app.UseApplicationSettings();
    Log.Information("Starting MngExtensions API");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "MngExtensions failed to start");
    throw;
}
finally
{
    Log.CloseAndFlush();
}
