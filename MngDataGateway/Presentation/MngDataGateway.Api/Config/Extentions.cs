using Asp.Versioning.ApiExplorer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Extensions.Options;
using MngDataGateway.Application.Configuration;
using Scalar.AspNetCore;
using Serilog;
using System.Net;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json.Serialization;
using static MngDataGateway.Application.Configuration.MngDataGatewaySettings;

namespace MngDataGateway.Api.Config
{
    public static class Extentions
    {
        public static Serilog.Core.Logger InitSerilog(this WebApplicationBuilder builder, MngDataGatewaySettings mngDataGatewaySettings)
        {

            //        Log.Logger = new LoggerConfiguration()
            //.ReadFrom.Configuration(builder.Configuration)
            //.CreateLogger();

            using var log = new LoggerConfiguration()
                .ReadFrom.Configuration(builder.Configuration) // önemli kısım!
                .CreateLogger();

            builder.Services.AddSingleton<Serilog.ILogger>(log);

            builder.Logging.ClearProviders();
            builder.Logging.AddSerilog(log);
            builder.Host.UseSerilog(log);

            log.Information($"DataGateway Starting. Version {Assembly.GetEntryAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion.Split('+')[0]} ");

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
            builder.Services.AddTransient<IConfigureOptions<Swashbuckle.AspNetCore.SwaggerGen.SwaggerGenOptions>, SwaggerConfigureOptions>();
        }

    public static void InitWebAPP(this WebApplicationBuilder builder, X509Certificate2 certificate)
    {
        // Get server settings from configuration
        var serverSettings = builder.Configuration.GetSection("MngDataGatewaySettings:Server").Get<ServerSettings>() 
            ?? new ServerSettings();

        builder.WebHost.ConfigureKestrel(options =>
        {
            options.AddServerHeader = false;

            // Parse host - if "0.0.0.0" or "*" listen on any IP
            if (serverSettings.Host == "0.0.0.0" || serverSettings.Host == "*")
            {
                options.ListenAnyIP(serverSettings.Port, _opt =>
                {
                    _opt.UseHttps(httpsOptions =>
                    {
                        httpsOptions.ServerCertificate = certificate;
                    });
                });
            }
            else if (serverSettings.Host == "localhost" || serverSettings.Host == "127.0.0.1")
            {
                options.ListenLocalhost(serverSettings.Port, _opt =>
                {
                    _opt.UseHttps(httpsOptions =>
                    {
                        httpsOptions.ServerCertificate = certificate;
                    });
                });
            }
            else
            {
                // Specific IP address
                options.Listen(System.Net.IPAddress.Parse(serverSettings.Host), serverSettings.Port, _opt =>
                {
                    _opt.UseHttps(httpsOptions =>
                    {
                        httpsOptions.ServerCertificate = certificate;
                    });
                });
            }

            // Log the configuration
            var logger = builder.Services.BuildServiceProvider().GetService<Serilog.ILogger>();
            logger?.Information($"Kestrel configured to listen on {serverSettings.Host}:{serverSettings.Port} ({serverSettings.Scheme})");
        });

            builder.Services.AddControllers().AddJsonOptions(o =>
            {
                o.JsonSerializerOptions.ReferenceHandler
                    = ReferenceHandler.IgnoreCycles;
                o.JsonSerializerOptions.MaxDepth = 64;
            });
            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle

            builder.Services.AddCors(l =>
            {
                l.AddPolicy("CorsPolicy", b =>
                    b.AllowAnyOrigin()
                    .AllowAnyMethod()
                    .AllowAnyHeader()
                     .WithExposedHeaders("Content-Disposition")
                    );
            });


            // Add services to the container
            builder.Services.AddControllers();
        }

        public static void InitAuthentication(this WebApplicationBuilder builder, MngDataGatewaySettings settings)
        {
            builder.Services.AddAuthentication("Bearer")
                .AddJwtBearer("Bearer", options =>
                {
                    options.Authority = settings.Actors.MngKeeper;

                    options.RequireHttpsMetadata = false;

                    options.BackchannelHttpHandler = new HttpClientHandler
                    {
                        ServerCertificateCustomValidationCallback = delegate { return true; }
                    };


                    options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
                    {
                        ValidateAudience = false,
                        ValidateIssuer = false,
                        ValidateIssuerSigningKey = false,
                        SignatureValidator = delegate (string token, Microsoft.IdentityModel.Tokens.TokenValidationParameters parameters)
                        {
                            //var jwt = new JwtSecurityToken(token);

                            var jwt = new Microsoft.IdentityModel.JsonWebTokens.JsonWebToken(token);

                            return jwt;
                        }
                    };

                });
        }

        public static void UseApplicationSettings(this WebApplication app, MngDataGatewaySettings mngDataGatewaySettings)
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

            // 2. HTTPS yönlendirmesi erkenden
            app.UseHttpsRedirection();

            // 3. Serilog ile request loglama
            app.UseSerilogRequestLogging(options =>
            {
                // options.RequestProjection = r => new { r.IsHttps, QueryString = r.QueryString.Value };
            });

            //if (app.Environment.IsDevelopment())
            {
                var apiVersionDescriptionProvider = app.Services.GetRequiredService<Asp.Versioning.ApiExplorer.IApiVersionDescriptionProvider>();
                
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
                            $"MngDataGateway API {description.GroupName.ToUpperInvariant()}");
                    }
                    
                    c.RoutePrefix = "swagger";
                    c.DocumentTitle = "MngDataGateway API Documentation";
                    c.DisplayRequestDuration();
                });
            }

            // 4. Routing middleware
            app.UseRouting();

            // 5. CORS
            app.UseCors("CorsPolicy");

            // 6. Authentication (kimlik doğrulama)
            app.UseAuthentication();

            // 7. Authorization (yetkilendirme)
            app.UseAuthorization();

            // 8. OpenAPI ve Scalar dökümantasyon endpointleri
            app.MapOpenApi();

            app.MapScalarApiReference(options =>
            {
                options
                    .WithTitle("MngDataGateway API")
                    .WithTheme(ScalarTheme.Purple)
                    .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient)
                    .WithOpenApiRoutePattern("/api-docs/{documentName}/swagger.json");

                options.AddServer(new ScalarServer(mngDataGatewaySettings.OpenApiServerPath, "MngDataGateway Server"));
            });

            // 9. Controller ve endpoint tanımlamaları (Map'ler en sonda)
            app.MapControllers().WithOpenApi();
        }
    }
}
