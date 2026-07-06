using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using MngReactor.Application;
using MngReactor.Application.Configuration;
using MngReactor.Infrastructure;
using MngReactor.Persistence;
using Serilog;

namespace MngReactor.Api.Config;

/// <summary>
/// Uygulama baslatma mantigi - Program ve integration testler tarafindan kullanilir.
/// </summary>
public static class AppBootstrapper
{
    /// <summary>
    /// WebApplication olusturur ve yapilandirir.
    /// options: Test ortami icin WebApplicationOptions (EnvironmentName = "Test" vb.).
    /// configureForTest: Build oncesi test ozel servislerini eklemek icin (ornegin mock'lar).
    /// </summary>
    public static WebApplication CreateApplication(
        string[] args,
        WebApplicationOptions? options = null,
        Action<WebApplicationBuilder>? configureForTest = null)
    {
        var builder = options != null ? WebApplication.CreateBuilder(options) : WebApplication.CreateBuilder(args);
        builder.Configuration.AddEnvironmentVariables();

        var settings = builder.Configuration.GetSection("MngReactorSettings").Get<MngReactorSettings>()
            ?? throw new InvalidOperationException("MngReactorSettings configuration required!");

        builder.Services.Configure<MngReactorSettings>(builder.Configuration.GetSection("MngReactorSettings"));

        var log = builder.InitSerilog(settings);
        builder.InitWebApp();

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

        builder.InitOpenApi();
        var isTestEnv = builder.Environment.EnvironmentName is "Test" or "Testing";
        if (configureForTest == null && !isTestEnv)
            builder.InitAuthentication(settings);

        builder.Services.AddHttpContextAccessor();
        builder.Services.AddHttpClient();
        builder.Services.AddMemoryCache();

        var legacySettings = settings.ToLegacy();
        builder.Services.AddSingleton<IOptions<MngReactor.Persistence.Settings.MngReactorSettings>>(Options.Create(legacySettings));

        builder.Services.AddApplicationServices(settings);
        builder.Services.AddPersistenceServices();
        builder.Services.AddInfrastructureServices(legacySettings);

        builder.Services.AddHealthChecks();

        configureForTest?.Invoke(builder);

        var app = builder.Build();

        app.UseApplicationSettings(settings);

        var mqttService = app.Services.GetService<MngReactor.Application.Services.MqttAppService>();
        if (mqttService != null && !string.IsNullOrEmpty(settings.Mqtt.Host))
        {
            try
            {
                mqttService.InitializeAsync().GetAwaiter().GetResult();
                Log.Information("MQTT baglantisi kuruldu");
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "MQTT baglantisi kurulamadi - uygulama MQTT olmadan devam ediyor.");
            }
        }

        Log.Information("Starting MngReactor API");
        return app;
    }
}
