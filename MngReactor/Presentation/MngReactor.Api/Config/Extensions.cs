using Asp.Versioning.ApiExplorer;
using MngReactor.Api.Middleware;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Extensions.Options;
using MngReactor.Application.Configuration;
using Scalar.AspNetCore;
using Serilog;
using System.Net;
using System.Reflection;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MngReactor.Api.Config;

public static class Extensions
{
    public static Serilog.Core.Logger InitSerilog(this WebApplicationBuilder builder, MngReactorSettings settings)
    {
        using var log = new LoggerConfiguration()
            .ReadFrom.Configuration(builder.Configuration)
            .CreateLogger();

        builder.Services.AddSingleton<Serilog.ILogger>(log);
        builder.Logging.ClearProviders();
        builder.Logging.AddSerilog(log);
        builder.Host.UseSerilog(log);

        log.Information("MngReactor Starting. Version {Version}",
            Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0]);

        return log;
    }

    public static void InitOpenApi(this WebApplicationBuilder builder)
    {
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(options =>
        {
            options.CustomSchemaIds(type => type.FullName);
        });
        builder.Services.AddTransient<IConfigureOptions<Swashbuckle.AspNetCore.SwaggerGen.SwaggerGenOptions>, SwaggerConfigureOptions>();
    }

    public static void InitWebApp(this WebApplicationBuilder builder)
    {
        var serverSettings = builder.Configuration.GetSection("MngReactorSettings:Server").Get<ServerSettings>()
            ?? new ServerSettings();

        builder.WebHost.ConfigureKestrel(options =>
        {
            options.AddServerHeader = false;
            if (serverSettings.Host == "0.0.0.0" || serverSettings.Host == "*")
                options.ListenAnyIP(serverSettings.Port);
            else if ((serverSettings.Host == "localhost" || serverSettings.Host == "127.0.0.1") && serverSettings.Port != 0)
                options.ListenLocalhost(serverSettings.Port);
            else if (serverSettings.Host == "localhost" || serverSettings.Host == "127.0.0.1")
                options.Listen(System.Net.IPAddress.Loopback, serverSettings.Port);
            else
                options.Listen(System.Net.IPAddress.Parse(serverSettings.Host), serverSettings.Port);
        });

        builder.Services.AddControllers().AddJsonOptions(o =>
        {
            o.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            o.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
            o.JsonSerializerOptions.MaxDepth = 64;
            // Config string gibi Base64 değerlerde + karakterinin \u002B olarak escape edilmemesi için
            o.JsonSerializerOptions.Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
        });
    }

    public static void InitAuthentication(this WebApplicationBuilder builder, MngReactorSettings settings)
    {
        builder.Services.AddAuthentication("Bearer")
            .AddJwtBearer("Bearer", options =>
            {
                options.RequireHttpsMetadata = false;
                options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
                {
                    ValidateAudience = false,
                    ValidateIssuer = false,
                    ValidateIssuerSigningKey = false,
                    SignatureValidator = (token, parameters) =>
                    {
                        var jwt = new Microsoft.IdentityModel.JsonWebTokens.JsonWebToken(token);
                        return jwt;
                    }
                };
                // Authority kaldırıldı: MngKeeper /.well-known/openid-configuration sunmuyor.
                // Token Keeper/Keycloak'tan; SignatureValidator ile doğrulama atlanıyor, metadata gerekmez.
            });
    }

    public static void UseApplicationSettings(this WebApplication app, MngReactorSettings settings)
    {
        app.UseGlobalExceptionHandler();
        app.UseSerilogRequestLogging();

        var apiVersionDescriptionProvider = app.Services.GetRequiredService<IApiVersionDescriptionProvider>();

        app.UseSwagger(c => c.RouteTemplate = "api-docs/{documentName}/swagger.json");
        app.UseSwaggerUI(c =>
        {
            foreach (var desc in apiVersionDescriptionProvider.ApiVersionDescriptions.OrderByDescending(d => d.ApiVersion))
            {
                c.SwaggerEndpoint($"/api-docs/{desc.GroupName}/swagger.json", $"MngReactor API {desc.GroupName.ToUpperInvariant()}");
            }
            c.RoutePrefix = "swagger";
            c.DocumentTitle = "MngReactor API Documentation";
            c.DisplayRequestDuration();
        });

        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseMiddleware<IngestDecryptMiddleware>();
        app.MapOpenApi();
        app.MapScalarApiReference(options =>
        {
            options.WithTitle("MngReactor API")
                .WithTheme(ScalarTheme.Purple)
                .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient)
                .WithOpenApiRoutePattern("/api-docs/{documentName}/swagger.json");
            if (!string.IsNullOrEmpty(settings.OpenApiServerPath))
                options.AddServer(new ScalarServer(settings.OpenApiServerPath, "MngReactor Server"));
        });
        app.MapControllers().WithOpenApi();
    }
}
