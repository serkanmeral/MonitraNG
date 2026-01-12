using Asp.Versioning.ApiExplorer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Extensions.Options;
using MngNotifier.Application.Configuration;
using Scalar.AspNetCore;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Linq;
using System.Net;
using System.Reflection;
using Serilog;
using static MngNotifier.Application.Configuration.MngNotifierSettings;

namespace MngNotifier.Api.Config
{
    public static class Extensions
    {
        public static Serilog.Core.Logger InitSerilog(this WebApplicationBuilder builder, MngNotifierSettings settings)
        {
            var log = new LoggerConfiguration()
                .ReadFrom.Configuration(builder.Configuration)
                .CreateLogger();

            builder.Services.AddSingleton<Serilog.ILogger>(log);

            builder.Logging.ClearProviders();
            builder.Logging.AddSerilog(log);
            builder.Host.UseSerilog(log);
            
            // Set static Log.Logger for Log.Information() to work
            Log.Logger = log;

            // Log application version on startup
            var version = Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            var versionString = version?.Split('+')[0] ?? "Unknown";
            log.Information("MngNotifier Starting. Version {Version}", versionString);

            return log;
        }

        public static void InitOpenApi(this WebApplicationBuilder builder)
        {
            builder.Services.AddEndpointsApiExplorer();
            
            // Swagger configuration with API versioning support
            builder.Services.AddSwaggerGen(options =>
            {
                // Use ApiExplorer to discover versioned APIs
                options.CustomSchemaIds(type => type.FullName);
            });
            
            // Register Swagger configure options for API versioning
            builder.Services.AddTransient<IConfigureOptions<SwaggerGenOptions>, SwaggerConfigureOptions>();
        }

        public static void InitWebAPP(this WebApplicationBuilder builder)
        {
            // Get server settings from configuration
            var serverSettings = builder.Configuration.GetSection("MngNotifierSettings:Server").Get<ServerSettings>() 
                ?? new ServerSettings();

            builder.WebHost.ConfigureKestrel(options =>
            {
                options.AddServerHeader = false;

                // Parse host - if "0.0.0.0" or "*" listen on any IP
                if (serverSettings.Host == "0.0.0.0" || serverSettings.Host == "*")
                {
                    options.ListenAnyIP(serverSettings.Port);
                }
                else if (serverSettings.Host == "localhost" || serverSettings.Host == "127.0.0.1")
                {
                    options.ListenLocalhost(serverSettings.Port);
                }
                else
                {
                    // Specific IP address
                    options.Listen(IPAddress.Parse(serverSettings.Host), serverSettings.Port);
                }

                // Log the configuration
                var logger = builder.Services.BuildServiceProvider().GetService<Serilog.ILogger>();
                logger?.Information($"Kestrel configured to listen on {serverSettings.Host}:{serverSettings.Port} ({serverSettings.Scheme})");
            });

            builder.Services.AddControllers().AddJsonOptions(o =>
            {
                o.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
                o.JsonSerializerOptions.MaxDepth = 64;
            });
        }

        public static void UseOpenApi(this WebApplication app)
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
                        $"MngNotifier API {description.GroupName.ToUpperInvariant()}");
                }
                
                c.RoutePrefix = "swagger";
                c.DocumentTitle = "MngNotifier API Documentation";
                c.DisplayRequestDuration();
            });

        }

        public static void UseApplicationSettings(this WebApplication app, MngNotifierSettings settings)
        {
            // 1. Global hata yakalama en yukarıda olmalı
            app.UseExceptionHandler(options =>
            {
                options.Run(async context =>
                {
                    context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                    context.Response.ContentType = "text/html";
                    var exceptionObject = context.Features.Get<IExceptionHandlerFeature>();
                    if (null != exceptionObject)
                    {
                        var errorMessage = $"{exceptionObject.Error.Message}";
                        await context.Response.WriteAsync(errorMessage).ConfigureAwait(false);
                    }
                });
            });

            // 2. Serilog ile request loglama
            app.UseSerilogRequestLogging(options =>
            {
                // options.RequestProjection = r => new { r.IsHttps, QueryString = r.QueryString.Value };
            });

            // 3. Swagger UI (Development'ta da production'da da açık)
            var apiVersionDescriptionProvider = app.Services.GetRequiredService<IApiVersionDescriptionProvider>();
            
            app.UseSwagger(c =>
            {
                c.RouteTemplate = "api-docs/{documentName}/swagger.json";
            });
            
            app.UseSwaggerUI(c =>
            {
                foreach (var description in apiVersionDescriptionProvider.ApiVersionDescriptions.OrderByDescending(d => d.ApiVersion))
                {
                    c.SwaggerEndpoint(
                        $"/api-docs/{description.GroupName}/swagger.json",
                        $"MngNotifier API {description.GroupName.ToUpperInvariant()}");
                }
                
                c.RoutePrefix = "swagger";
                c.DocumentTitle = "MngNotifier API Documentation";
                c.DisplayRequestDuration();
            });

            // 4. Routing middleware
            app.UseRouting();

            // 5. OpenAPI ve Scalar dökümantasyon endpointleri
            app.MapOpenApi();

            app.MapScalarApiReference(options =>
            {
                options
                    .WithTitle("MngNotifier API")
                    .WithTheme(ScalarTheme.Purple)
                    .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient)
                    .WithOpenApiRoutePattern("/api-docs/{documentName}/swagger.json");
            });

            // 6. Controller ve endpoint tanımlamaları (Map'ler en sonda)
            app.MapControllers().WithOpenApi();
        }
    }
}
